namespace Ganjj.Order.Application.Abstractions;

public interface IProductStockClient
{
    Task DecrementAsync(
        string productId,
        int quantity,
        string operationId,
        string correlationId,
        CancellationToken cancellationToken);

    Task RestoreAsync(
        string productId,
        int quantity,
        string operationId,
        string compensatesOperationId,
        string correlationId,
        CancellationToken cancellationToken);
}
