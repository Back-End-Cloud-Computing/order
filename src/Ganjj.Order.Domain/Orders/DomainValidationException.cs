namespace Ganjj.Order.Domain.Orders;

public sealed class DomainValidationException(string message) : ArgumentException(message);
