using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Infrastructure.Configuration;
using Ganjj.Order.Infrastructure.Http;
using Ganjj.Order.Infrastructure.Messaging;
using Ganjj.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ganjj.Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OrderDatabase")
                               ?? throw new InvalidOperationException(
                                   "ConnectionStrings:OrderDatabase is required.");
        services.AddDbContext<OrderDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddSingleton<IIdentifierGenerator, GuidIdentifierGenerator>();

        services.Configure<CartServiceOptions>(configuration.GetSection(CartServiceOptions.SectionName));
        services.Configure<ProductServiceOptions>(configuration.GetSection(ProductServiceOptions.SectionName));
        services.Configure<SupportServiceOptions>(configuration.GetSection(SupportServiceOptions.SectionName));
        services.Configure<InternalServiceOptions>(configuration.GetSection(InternalServiceOptions.SectionName));
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(options => !options.Enabled ||
                                 (!string.IsNullOrWhiteSpace(options.HostName) &&
                                  !string.IsNullOrWhiteSpace(options.UserName) &&
                                  !string.IsNullOrWhiteSpace(options.Password) &&
                                  !string.IsNullOrWhiteSpace(options.Exchange) &&
                                  !string.IsNullOrWhiteSpace(options.RoutingKey) &&
                                  options.Port is > 0 and <= 65535 &&
                                  options.PollIntervalMilliseconds >= 100 &&
                                  options.LeaseSeconds > 0 &&
                                  options.BatchSize > 0),
                "RabbitMq configuration is invalid while the publisher is enabled.")
            .ValidateOnStart();

        if (configuration.GetValue<bool>($"{RabbitMqOptions.SectionName}:Enabled"))
        {
            services.AddScoped<IOutboxStore, OutboxStore>();
            services.AddScoped<OutboxProcessor>();
            services.AddSingleton<IOrderConfirmedPublisher, RabbitMqOrderConfirmedPublisher>();
            services.AddHostedService<OutboxBackgroundService>();
        }

        services.AddHttpClient<ICartClient, CartClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<CartServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("Resilience:HttpTimeoutSeconds", 3));
        });
        services.AddHttpClient<IProductStockClient, ProductStockClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<ProductServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("Resilience:HttpTimeoutSeconds", 3));
        });
        services.AddHttpClient<IOrderCreatedNotifier, SupportNotificationClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<SupportServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(configuration.GetValue("Resilience:SupportTimeoutSeconds", 2));
        });

        return services;
    }

    private sealed class GuidIdentifierGenerator : IIdentifierGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
