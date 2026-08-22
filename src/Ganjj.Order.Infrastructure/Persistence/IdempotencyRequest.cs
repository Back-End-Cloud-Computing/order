using Ganjj.Order.Application.Abstractions;

namespace Ganjj.Order.Infrastructure.Persistence;

internal sealed class IdempotencyRequest
{
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public IdempotencyStatus Status { get; set; }
    public Guid? OrderId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
