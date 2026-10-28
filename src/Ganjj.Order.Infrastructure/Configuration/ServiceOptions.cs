namespace Ganjj.Order.Infrastructure.Configuration;

public sealed class CartServiceOptions
{
    public const string SectionName = "Services:Cart";
    public string BaseUrl { get; init; } = "http://localhost:3003";
}

public sealed class ProductServiceOptions
{
    public const string SectionName = "Services:Product";
    public string BaseUrl { get; init; } = "http://localhost:8000";
}

public sealed class SupportServiceOptions
{
    public const string SectionName = "Services:Support";
    public string BaseUrl { get; init; } = "http://localhost:8090";
}

public sealed class InternalServiceOptions
{
    public const string SectionName = "Services:Internal";
    public string Secret { get; init; } = string.Empty;
}

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public bool Enabled { get; init; }
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
    public string Exchange { get; init; } = "ganjj.orders";
    public string RoutingKey { get; init; } = "order.confirmed";
    public int PollIntervalMilliseconds { get; init; } = 1000;
    public int LeaseSeconds { get; init; } = 30;
    public int BatchSize { get; init; } = 20;
}
