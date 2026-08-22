using Ganjj.Order.Domain.Orders;
using Ganjj.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class SqlServerPersistenceTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlServer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
    private DbContextOptions<OrderDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _sqlServer.StartAsync();
        _options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer(_sqlServer.GetConnectionString())
            .Options;

        await using var dbContext = new OrderDbContext(_options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _sqlServer.DisposeAsync();

    [Fact]
    public async Task OrderAggregate_WhenPersisted_ShouldRestoreItemsAddressAndVersion()
    {
        var orderId = Guid.NewGuid();
        var order = OrderAggregate.Create(
            orderId,
            42,
            "Customer",
            "customer@ganjj.test",
            new DeliveryAddress("Cloud Street", "10", null, "Center", "Curitiba", "PR", "80000-000", "BR"),
            [OrderItem.Create(Guid.NewGuid(), orderId, 7, "Cloud Product", "M", null, 2, 29.90m, 0m, null, null)],
            DateTimeOffset.UtcNow);

        await using (var writeContext = new OrderDbContext(_options))
        {
            writeContext.Orders.Add(order);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new OrderDbContext(_options);
        var persisted = await readContext.Orders.Include(value => value.Items).SingleAsync();

        Assert.Equal(42, persisted.CustomerId);
        Assert.Equal("Curitiba", persisted.DeliveryAddress.City);
        Assert.Equal(59.80m, persisted.Total);
        Assert.Single(persisted.Items);
        Assert.NotEmpty(persisted.Version);
    }
}
