namespace Ganjj.Order.Application.Abstractions;

public interface IProductStockClient
{
    Task DecrementAsync(
        long productId,
        int quantity,
        string operationId,
        string correlationId,
        CancellationToken cancellationToken);

    Task RestoreAsync(
        long productId,
        int quantity,
        string operationId,
        string compensatesOperationId,
        string correlationId,
        CancellationToken cancellationToken);
}
