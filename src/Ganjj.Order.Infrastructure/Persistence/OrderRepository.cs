using Ganjj.Order.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Infrastructure.Persistence;

internal sealed class OrderRepository(OrderDbContext dbContext) : IOrderRepository
{
    public async Task<IdempotencySnapshot?> FindIdempotencyAsync(
        string key,
        CancellationToken cancellationToken) =>
        await dbContext.IdempotencyRequests
            .AsNoTracking()
            .Where(request => request.Key == key)
            .Select(request => new IdempotencySnapshot(
                request.Key, request.RequestHash, request.CustomerId, request.Status, request.OrderId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> TryStartAsync(
        string key,
        string requestHash,
        long customerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var request = new IdempotencyRequest
        {
            Key = key,
            RequestHash = requestHash,
            CustomerId = customerId,
            Status = IdempotencyStatus.InProgress,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.IdempotencyRequests.Add(request);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(request).State = EntityState.Detached;
            return false;
        }
    }

    public async Task SaveAndCompleteAsync(
        OrderAggregate order,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.Orders.Add(order);
        var request = await dbContext.IdempotencyRequests
            .SingleAsync(value => value.Key == idempotencyKey, cancellationToken);
        request.Status = IdempotencyStatus.Completed;
        request.OrderId = order.Id;
        request.UpdatedAt = order.UpdatedAt;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RemoveIdempotencyAsync(string key, CancellationToken cancellationToken)
    {
        var request = await dbContext.IdempotencyRequests
            .SingleOrDefaultAsync(value => value.Key == key, cancellationToken);
        if (request is null) return;
        dbContext.IdempotencyRequests.Remove(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<OrderAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Orders.Include(order => order.Items)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OrderAggregate>> ListForCustomerAsync(
        long customerId,
        CancellationToken cancellationToken) =>
        await dbContext.Orders.AsNoTracking().Include(order => order.Items)
            .Where(order => order.CustomerId == customerId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<OrderAggregate>> ListAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Orders.AsNoTracking().Include(order => order.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
