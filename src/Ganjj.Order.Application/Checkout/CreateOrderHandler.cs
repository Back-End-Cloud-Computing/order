using System.Security.Cryptography;
using System.Text;
using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Domain.Orders;
using Microsoft.Extensions.Logging;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Application.Checkout;

public sealed class CreateOrderHandler(
    IOrderRepository repository,
    ICartClient cartClient,
    IProductStockClient stockClient,
    IOrderCreatedNotifier orderCreatedNotifier,
    TimeProvider timeProvider,
    IIdentifierGenerator identifiers,
    ILogger<CreateOrderHandler> logger)
{
    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        ValidateCommand(command);
        var requestHash = ComputeRequestHash(command);
        var existing = await repository.FindIdempotencyAsync(command.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ResolveExistingAsync(existing, requestHash, command.CustomerId, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        var acquired = await repository.TryStartAsync(
            command.IdempotencyKey, requestHash, command.CustomerId, now, cancellationToken);

        if (!acquired)
        {
            existing = await repository.FindIdempotencyAsync(command.IdempotencyKey, cancellationToken);
            if (existing is null)
            {
                throw new IdempotencyConflictException("The idempotency key could not be acquired.");
            }

            return await ResolveExistingAsync(existing, requestHash, command.CustomerId, cancellationToken);
        }

        var correlationId = string.IsNullOrWhiteSpace(command.CorrelationId)
            ? command.IdempotencyKey
            : command.CorrelationId;
        var decremented = new List<DecrementedStock>();

        try
        {
            var cart = await cartClient.GetCurrentAsync(
                command.CustomerId, command.BearerToken, correlationId, cancellationToken);
            ValidateCart(cart);

            var orderId = identifiers.NewId();
            var orderCreatedEventId = identifiers.NewId();
            var items = cart.Items.Select(item => OrderItem.Create(
                identifiers.NewId(),
                orderId,
                item.ProductId,
                item.ProductName,
                item.Color,
                item.ImageUrl,
                item.Quantity,
                item.UnitListPrice,
                item.UnitListPrice - item.UnitFinalPrice,
                null,
                null)).ToList();

            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                var operationId = OperationId(command.IdempotencyKey, "decrement", item.ProductId, index);
                await stockClient.DecrementAsync(
                    item.ProductId, item.Quantity, operationId, correlationId, cancellationToken);
                decremented.Add(new DecrementedStock(item.ProductId, item.Quantity, operationId, index));
            }

            var order = OrderAggregate.Create(
                orderId,
                command.CustomerId,
                command.CustomerName,
                command.CustomerEmail,
                command.DeliveryAddress,
                items,
                now,
                orderCreatedEventId: orderCreatedEventId);

            await repository.SaveAndCompleteAsync(order, command.IdempotencyKey, cancellationToken);
            await TryNotifySupportAsync(order, correlationId, cancellationToken);
            return CreateOrderResult.From(order);
        }
        catch (Exception checkoutFailure)
        {
            Exception? compensationFailure = null;
            foreach (var stock in decremented.AsEnumerable().Reverse())
            {
                try
                {
                    var compensationId = OperationId(
                        command.IdempotencyKey, "restore", stock.ProductId, stock.Index);
                    await stockClient.RestoreAsync(
                        stock.ProductId,
                        stock.Quantity,
                        compensationId,
                        stock.OperationId,
                        correlationId,
                        CancellationToken.None);
                }
                catch (Exception error)
                {
                    compensationFailure ??= error;
                }
            }

            if (compensationFailure is not null)
            {
                throw new CompensationFailedException(checkoutFailure, compensationFailure);
            }

            await repository.RemoveIdempotencyAsync(command.IdempotencyKey, CancellationToken.None);
            throw;
        }
    }

    private async Task TryNotifySupportAsync(
        OrderAggregate order,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            logger.LogWarning(
                "Order {OrderId} was created without an email claim; Support notification {EventId} was skipped.",
                order.Id,
                order.OrderCreatedEventId);
            return;
        }

        try
        {
            await orderCreatedNotifier.NotifyAsync(new OrderCreatedNotification(
                order.OrderCreatedEventId,
                order.CreatedAt,
                order.Id,
                order.Code,
                order.CustomerId,
                order.CustomerEmail,
                null,
                order.Total,
                "BRL",
                correlationId), cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Order {OrderId} was created, but Support notification {EventId} failed.",
                order.Id,
                order.OrderCreatedEventId);
        }
    }

    public static string ComputeRequestHash(CreateOrderCommand command)
    {
        var address = command.DeliveryAddress;
        var canonical = string.Join('|',
            command.CustomerId.ToString("D"),
            address.Street,
            address.Number,
            address.Complement ?? string.Empty,
            address.District,
            address.City,
            address.State,
            address.PostalCode,
            address.Country);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private async Task<CreateOrderResult> ResolveExistingAsync(
        IdempotencySnapshot existing,
        string requestHash,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal) ||
            existing.CustomerId != customerId)
        {
            throw new IdempotencyConflictException(
                "The idempotency key was already used with a different request.");
        }

        if (existing.Status != IdempotencyStatus.Completed || existing.OrderId is null)
        {
            throw new IdempotencyConflictException("The original request is still in progress.");
        }

        var order = await repository.GetByIdAsync(existing.OrderId.Value, cancellationToken);
        if (order is null)
        {
            throw new IdempotencyConflictException("The completed idempotent result could not be loaded.");
        }

        return CreateOrderResult.From(order);
    }

    private static void ValidateCommand(CreateOrderCommand command)
    {
        if (command.CustomerId == Guid.Empty)
        {
            throw new DomainValidationException("CustomerId is required.");
        }

        if (string.IsNullOrWhiteSpace(command.BearerToken))
        {
            throw new DomainValidationException("Bearer token is required for the current Cart contract.");
        }

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length is < 8 or > 100)
        {
            throw new DomainValidationException("IdempotencyKey must contain between 8 and 100 characters.");
        }
    }

    private static void ValidateCart(CartSnapshot cart)
    {
        ArgumentNullException.ThrowIfNull(cart);
        if (cart.Items.Count == 0)
        {
            throw new CartEmptyException();
        }

        if (cart.Items.Any(item => string.IsNullOrWhiteSpace(item.ProductId) || item.ProductId.Trim().Length > 64 ||
                                   item.Quantity <= 0 || item.UnitListPrice < 0 || item.UnitFinalPrice < 0 ||
                                   item.UnitFinalPrice > item.UnitListPrice ||
                                   string.IsNullOrWhiteSpace(item.ProductName)))
        {
            throw new InvalidDependencyContractException("Cart", "an item snapshot is malformed");
        }
    }

    private static string OperationId(string key, string action, string productId, int index)
    {
        var input = $"{key}:{action}:{productId}:{index}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..32];
    }

    private sealed record DecrementedStock(string ProductId, int Quantity, string OperationId, int Index);
}
