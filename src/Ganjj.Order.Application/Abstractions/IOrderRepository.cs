using Ganjj.Order.Domain.Orders;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Application.Abstractions;

public enum IdempotencyStatus
{
    InProgress,
    Completed,
    Failed
}

public sealed record IdempotencySnapshot(
    string Key,
    string RequestHash,
    Guid CustomerId,
    IdempotencyStatus Status,
    Guid? OrderId);

public interface IOrderRepository
{
    Task<IdempotencySnapshot?> FindIdempotencyAsync(string key, CancellationToken cancellationToken);

    Task<bool> TryStartAsync(
        string key,
        string requestHash,
        Guid customerId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task SaveAndCompleteAsync(
        OrderAggregate order,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task RemoveIdempotencyAsync(string key, CancellationToken cancellationToken);

    Task<OrderAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderAggregate>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderAggregate>> ListAllAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
