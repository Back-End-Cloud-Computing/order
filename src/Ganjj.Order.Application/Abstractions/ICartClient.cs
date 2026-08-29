using Ganjj.Order.Application.Checkout;

namespace Ganjj.Order.Application.Abstractions;

public interface ICartClient
{
    Task<CartSnapshot> GetCurrentAsync(
        Guid customerId,
        string bearerToken,
        string correlationId,
        CancellationToken cancellationToken);
}
