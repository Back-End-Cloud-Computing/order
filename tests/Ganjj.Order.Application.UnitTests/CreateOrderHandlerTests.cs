using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Application.Checkout;
using Ganjj.Order.Domain.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Application.UnitTests;

public sealed class CreateOrderHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Handle_WhenCartIsValid_ShouldPersistSnapshotAfterStockDecrements()
    {
        var repository = new FakeOrderRepository();
        var stock = new FakeStockClient();
        var cart = new FakeCartClient(Cart(Item("sku-12", 2, 50m), Item("sku-34", 1, 25m)));
        var handler = new CreateOrderHandler(
            repository, cart, stock, new FakeOrderCreatedNotifier(), new FixedTimeProvider(Now),
            new SequenceIds(), NullLogger<CreateOrderHandler>.Instance);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(125m, result.Total);
        Assert.Equal(["sku-12", "sku-34"], stock.Decremented.Select(call => call.ProductId));
        Assert.NotNull(repository.SavedOrder);
        Assert.Equal(2, repository.SavedOrder.Items.Count);
        Assert.Equal(IdempotencyStatus.Completed, repository.Entry?.Status);
        Assert.Equal("token", cart.BearerToken);
    }

    [Fact]
    public async Task Handle_WhenOrderIsPersisted_ShouldNotifySupportWithStableOrderCreatedEvent()
    {
        var repository = new FakeOrderRepository();
        var notifier = new FakeOrderCreatedNotifier(() => Assert.NotNull(repository.SavedOrder));
        var handler = Handler(repository, new FakeStockClient(), Cart(Item("sku-12", 2, 50m)), notifier);

        var result = await handler.HandleAsync(Command() with { CorrelationId = "correlation-123" },
            CancellationToken.None);

        var notification = Assert.Single(notifier.Notifications);
        Assert.Equal(repository.SavedOrder!.OrderCreatedEventId, notification.EventId);
        Assert.Equal(result.Id, notification.OrderId);
        Assert.Equal(result.Code, notification.OrderCode);
        Assert.Equal(CustomerId, notification.CustomerId);
        Assert.Equal("customer@example.com", notification.CustomerEmail);
        Assert.Equal(100m, notification.Total);
        Assert.Equal("BRL", notification.Currency);
        Assert.Equal("correlation-123", notification.CorrelationId);
        Assert.Equal(Now, notification.OccurredAt);
    }

    [Fact]
    public async Task Handle_WhenCartHasPromotion_ShouldPersistListAndDiscountedPrices()
    {
        var repository = new FakeOrderRepository();
        var discountedItem = new CartItemSnapshot(
            "product-abc", "Camiseta GANJJ", "Preto", null, 2, 100m, 90m);
        var handler = Handler(repository, new FakeStockClient(), Cart(discountedItem));

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(180m, result.Total);
        var persisted = Assert.Single(repository.SavedOrder!.Items);
        Assert.Equal(100m, persisted.UnitListPrice);
        Assert.Equal(10m, persisted.UnitDiscount);
        Assert.Equal(90m, persisted.UnitFinalPrice);
        Assert.Equal("Preto", persisted.Color);
    }

    [Fact]
    public async Task Handle_WhenSupportNotificationFails_ShouldKeepCompletedOrderAndNotCompensateStock()
    {
        var repository = new FakeOrderRepository();
        var stock = new FakeStockClient();
        var notifier = new FakeOrderCreatedNotifier
        {
            Failure = new HttpRequestException("Support is unavailable.")
        };
        var handler = Handler(repository, stock, Cart(Item("sku-12", 1, 50m)), notifier);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(repository.SavedOrder!.Id, result.Id);
        Assert.Equal(IdempotencyStatus.Completed, repository.Entry?.Status);
        Assert.Empty(stock.Restored);
        Assert.Single(notifier.Notifications);
    }

    [Fact]
    public async Task Handle_WhenSecondStockDecrementFails_ShouldRestoreFirstAndNotPersistOrder()
    {
        var repository = new FakeOrderRepository();
        var stock = new FakeStockClient { FailProductId = "sku-34" };
        var handler = Handler(repository, stock, Cart(Item("sku-12", 1, 50m), Item("sku-34", 1, 25m)));

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            handler.HandleAsync(Command(), CancellationToken.None));

        Assert.Equal(["sku-12"], stock.Restored.Select(call => call.ProductId));
        Assert.Null(repository.SavedOrder);
        Assert.Null(repository.Entry);
    }

    [Fact]
    public async Task Handle_WhenCartIsEmpty_ShouldNotChangeStock()
    {
        var repository = new FakeOrderRepository();
        var stock = new FakeStockClient();
        var handler = Handler(repository, stock, new CartSnapshot([]));

        await Assert.ThrowsAsync<CartEmptyException>(() =>
            handler.HandleAsync(Command(), CancellationToken.None));

        Assert.Empty(stock.Decremented);
        Assert.Null(repository.Entry);
    }

    [Fact]
    public async Task Handle_WhenCompletedIdempotencyKeyIsRepeated_ShouldReturnOriginalWithoutDependencies()
    {
        var repository = new FakeOrderRepository();
        var originalId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var original = ExistingOrder(originalId);
        repository.SeedCompleted("checkout-key", CreateOrderHandler.ComputeRequestHash(Command()), original);
        var cart = new FakeCartClient(Cart(Item("sku-99", 1, 999m)));
        var stock = new FakeStockClient();
        var handler = new CreateOrderHandler(
            repository, cart, stock, new FakeOrderCreatedNotifier(), new FixedTimeProvider(Now),
            new SequenceIds(), NullLogger<CreateOrderHandler>.Instance);

        var result = await handler.HandleAsync(Command(), CancellationToken.None);

        Assert.Equal(originalId, result.Id);
        Assert.Equal(10m, result.Total);
        Assert.Equal(0, cart.Calls);
        Assert.Empty(stock.Decremented);
    }

    private static CreateOrderHandler Handler(
        FakeOrderRepository repository,
        FakeStockClient stock,
        CartSnapshot cart,
        FakeOrderCreatedNotifier? notifier = null) =>
        new(repository, new FakeCartClient(cart), stock, notifier ?? new FakeOrderCreatedNotifier(),
            new FixedTimeProvider(Now), new SequenceIds(), NullLogger<CreateOrderHandler>.Instance);

    private static CreateOrderCommand Command() => new(
        CustomerId,
        null,
        "customer@example.com",
        "token",
        "checkout-key",
        new DeliveryAddress("Rua A", "1", null, "Centro", "Curitiba", "PR", "80000-000", "BR"));

    private static CartSnapshot Cart(params CartItemSnapshot[] items) => new(items);

    private static CartItemSnapshot Item(string id, int quantity, decimal price) =>
        new(id, $"Product {id}", null, null, quantity, price, price);

    private static OrderAggregate ExistingOrder(Guid id)
    {
        var item = OrderItem.Create(Guid.NewGuid(), id, "sku-12", "Product 12", null, null, 1, 10m, 0m, null, null);
        return OrderAggregate.Create(id, CustomerId, null, "customer@example.com",
            new DeliveryAddress("Rua A", "1", null, "Centro", "Curitiba", "PR", "80000-000", "BR"),
            [item], Now);
    }

    private sealed class FakeCartClient(CartSnapshot result) : ICartClient
    {
        public int Calls { get; private set; }
        public string? BearerToken { get; private set; }

        public Task<CartSnapshot> GetCurrentAsync(Guid customerId, string bearerToken, string correlationId,
            CancellationToken cancellationToken)
        {
            Calls++;
            BearerToken = bearerToken;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeStockClient : IProductStockClient
    {
        public string? FailProductId { get; init; }
        public List<StockCall> Decremented { get; } = [];
        public List<StockCall> Restored { get; } = [];

        public Task DecrementAsync(string productId, int quantity, string operationId, string correlationId,
            CancellationToken cancellationToken)
        {
            if (productId == FailProductId)
            {
                throw new InsufficientStockException(productId, null);
            }

            Decremented.Add(new StockCall(productId, quantity, operationId));
            return Task.CompletedTask;
        }

        public Task RestoreAsync(string productId, int quantity, string operationId, string compensatesOperationId,
            string correlationId, CancellationToken cancellationToken)
        {
            Restored.Add(new StockCall(productId, quantity, operationId));
            return Task.CompletedTask;
        }
    }

    private sealed record StockCall(string ProductId, int Quantity, string OperationId);

    private sealed class FakeOrderCreatedNotifier(Action? beforeNotify = null) : IOrderCreatedNotifier
    {
        public Exception? Failure { get; init; }
        public List<OrderCreatedNotification> Notifications { get; } = [];

        public Task NotifyAsync(OrderCreatedNotification notification, CancellationToken cancellationToken)
        {
            beforeNotify?.Invoke();
            Notifications.Add(notification);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        public IdempotencySnapshot? Entry { get; private set; }
        public OrderAggregate? SavedOrder { get; private set; }
        private OrderAggregate? _completedOrder;

        public Task<IdempotencySnapshot?> FindIdempotencyAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult(Entry);

        public Task<bool> TryStartAsync(string key, string requestHash, Guid customerId,
            DateTimeOffset now, CancellationToken cancellationToken)
        {
            if (Entry is not null) return Task.FromResult(false);
            Entry = new IdempotencySnapshot(key, requestHash, customerId, IdempotencyStatus.InProgress, null);
            return Task.FromResult(true);
        }

        public Task SaveAndCompleteAsync(OrderAggregate order, string idempotencyKey,
            CancellationToken cancellationToken)
        {
            SavedOrder = order;
            _completedOrder = order;
            Entry = Entry! with { Status = IdempotencyStatus.Completed, OrderId = order.Id };
            return Task.CompletedTask;
        }

        public Task RemoveIdempotencyAsync(string key, CancellationToken cancellationToken)
        {
            Entry = null;
            return Task.CompletedTask;
        }

        public Task<OrderAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_completedOrder?.Id == id ? _completedOrder : null);

        public Task<IReadOnlyList<OrderAggregate>> ListForCustomerAsync(
            Guid customerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OrderAggregate>>(
                _completedOrder?.CustomerId == customerId ? [_completedOrder] : []);

        public Task<IReadOnlyList<OrderAggregate>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OrderAggregate>>(_completedOrder is null ? [] : [_completedOrder]);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void SeedCompleted(string key, string hash, OrderAggregate order)
        {
            Entry = new IdempotencySnapshot(key, hash, order.CustomerId, IdempotencyStatus.Completed, order.Id);
            _completedOrder = order;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SequenceIds : IIdentifierGenerator
    {
        private int _next;
        public Guid NewId() => Guid.Parse($"00000000-0000-0000-0000-{++_next:000000000000}");
    }
}
