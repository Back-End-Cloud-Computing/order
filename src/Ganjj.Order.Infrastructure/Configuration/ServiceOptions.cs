namespace Ganjj.Order.Infrastructure.Configuration;

public sealed class CartServiceOptions
{
    public const string SectionName = "Services:Cart";
    public string BaseUrl { get; init; } = "http://localhost:3003";
}

public sealed class ProductServiceOptions
{
    public const string SectionName = "Services:Product";
    public string BaseUrl { get; init; } = "http://localhost:3002";
}

public sealed class InternalServiceOptions
{
    public const string SectionName = "Services:Internal";
    public string Secret { get; init; } = string.Empty;
}
