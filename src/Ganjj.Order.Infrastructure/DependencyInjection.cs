using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Infrastructure.Configuration;
using Ganjj.Order.Infrastructure.Http;
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
        services.Configure<InternalServiceOptions>(configuration.GetSection(InternalServiceOptions.SectionName));

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

        return services;
    }

    private sealed class GuidIdentifierGenerator : IIdentifierGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
