namespace Ganjj.Order.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public string? CustomerName { get; private set; }
    public string? CustomerEmail { get; private set; }
    public Guid? QuoteId { get; private set; }
    public long? CartVersion { get; private set; }
    public OrderStatus Status { get; private set; }
    public int TrackingStep { get; private set; }
    public decimal Total { get; private set; }
    public DeliveryAddress DeliveryAddress { get; private set; } = null!;
    public string? TrackingNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public static Order Create(
        Guid id,
        Guid customerId,
        string? customerName,
        string? customerEmail,
        DeliveryAddress deliveryAddress,
        IEnumerable<OrderItem> items,
        DateTimeOffset now,
        Guid? quoteId = null,
        long? cartVersion = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Order Id is required.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainValidationException("CustomerId is required.");
        }

        ArgumentNullException.ThrowIfNull(deliveryAddress);
        ArgumentNullException.ThrowIfNull(items);

        var snapshots = items.ToList();
        if (snapshots.Count == 0)
        {
            throw new DomainValidationException("Order must contain at least one item.");
        }

        if (snapshots.Any(item => item.OrderId != id))
        {
            throw new DomainValidationException("Every item must belong to the Order.");
        }

        var order = new Order
        {
            Id = id,
            Code = $"#GNJ-{now:yyyyMMdd}-{id:N}"[..22].ToUpperInvariant(),
            CustomerId = customerId,
            CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim(),
            CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail.Trim(),
            QuoteId = quoteId,
            CartVersion = cartVersion,
            Status = OrderStatus.Pending,
            TrackingStep = 0,
            Total = snapshots.Sum(item => item.LineTotal),
            DeliveryAddress = deliveryAddress,
            CreatedAt = now.ToUniversalTime(),
            UpdatedAt = now.ToUniversalTime()
        };

        order._items.AddRange(snapshots);
        return order;
    }

    public void UpdateTracking(int step, string? trackingNumber, DateTimeOffset now)
    {
        if (step is < 0 or > 5)
        {
            throw new DomainValidationException("Tracking step must be between 0 and 5.");
        }

        TrackingStep = step;
        Status = step switch
        {
            <= 2 => OrderStatus.Pending,
            <= 4 => OrderStatus.Shipping,
            _ => OrderStatus.Completed
        };
        TrackingNumber = string.IsNullOrWhiteSpace(trackingNumber) ? null : trackingNumber.Trim();
        UpdatedAt = now.ToUniversalTime();
    }
}
