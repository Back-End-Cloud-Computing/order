namespace Ganjj.Order.Domain.Orders;

public sealed class DeliveryAddress
{
    private DeliveryAddress()
    {
    }

    public DeliveryAddress(
        string street,
        string number,
        string? complement,
        string district,
        string city,
        string state,
        string postalCode,
        string country)
    {
        Street = Required(street, nameof(Street), 200);
        Number = Required(number, nameof(Number), 30);
        Complement = Optional(complement, 100);
        District = Required(district, nameof(District), 100);
        City = Required(city, nameof(City), 100);
        State = Required(state, nameof(State), 50);
        PostalCode = Required(postalCode, nameof(PostalCode), 20);
        Country = Required(country, nameof(Country), 2).ToUpperInvariant();
    }

    public string Street { get; private set; } = string.Empty;
    public string Number { get; private set; } = string.Empty;
    public string? Complement { get; private set; }
    public string District { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;

    private static string Required(string value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainValidationException($"{field} is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainValidationException($"{field} must contain at most {maxLength} characters.");
        }

        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new DomainValidationException($"Value must contain at most {maxLength} characters.");
        }

        return normalized;
    }
}
