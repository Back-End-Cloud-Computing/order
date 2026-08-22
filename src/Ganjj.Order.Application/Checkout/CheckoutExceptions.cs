namespace Ganjj.Order.Application.Checkout;

public abstract class OrderApplicationException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

public sealed class CartEmptyException()
    : OrderApplicationException("cart_empty", "The authenticated customer's cart is empty.");

public sealed class IdempotencyConflictException(string message)
    : OrderApplicationException("idempotency_conflict", message);

public sealed class InsufficientStockException(long productId, int? availableStock)
    : OrderApplicationException("insufficient_stock", $"Product {productId} has insufficient stock.")
{
    public long ProductId { get; } = productId;
    public int? AvailableStock { get; } = availableStock;
}

public sealed class ProductNotFoundException(long productId)
    : OrderApplicationException("product_not_found", $"Product {productId} was not found.")
{
    public long ProductId { get; } = productId;
}

public sealed class DependencyUnavailableException(string dependency, Exception? innerException = null)
    : OrderApplicationException("dependency_unavailable", $"{dependency} is currently unavailable.", innerException)
{
    public string Dependency { get; } = dependency;
}

public sealed class InvalidDependencyContractException(string dependency, string detail)
    : OrderApplicationException("invalid_dependency_contract", $"{dependency} returned invalid data: {detail}");

public sealed class CompensationFailedException(Exception checkoutFailure, Exception compensationFailure)
    : OrderApplicationException(
        "compensation_failed",
        "Checkout failed and at least one stock compensation could not be confirmed.",
        new AggregateException(checkoutFailure, compensationFailure));

public sealed class OrderNotFoundException(Guid orderId)
    : OrderApplicationException("order_not_found", $"Order {orderId} was not found.");

public sealed class OrderForbiddenException()
    : OrderApplicationException("order_forbidden", "The authenticated customer cannot access this order.");
