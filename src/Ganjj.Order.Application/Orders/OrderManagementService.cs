using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Application.Checkout;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Application.Orders;

public sealed class OrderManagementService(IOrderRepository repository, TimeProvider timeProvider)
{
    public Task<IReadOnlyList<OrderAggregate>> ListMineAsync(long customerId, CancellationToken cancellationToken) =>
        repository.ListForCustomerAsync(customerId, cancellationToken);

    public Task<IReadOnlyList<OrderAggregate>> ListAllAsync(CancellationToken cancellationToken) =>
        repository.ListAllAsync(cancellationToken);

    public async Task<OrderAggregate> GetAsync(
        Guid orderId,
        long customerId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(orderId, cancellationToken)
                    ?? throw new OrderNotFoundException(orderId);
        if (!isAdmin && order.CustomerId != customerId)
        {
            throw new OrderForbiddenException();
        }

        return order;
    }

    public async Task<OrderAggregate> UpdateTrackingAsync(
        Guid orderId,
        int step,
        string? trackingNumber,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(orderId, cancellationToken)
                    ?? throw new OrderNotFoundException(orderId);
        order.UpdateTracking(step, trackingNumber, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return order;
    }
}
