using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ganjj.Order.Infrastructure.Persistence;

public sealed class OrderDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__OrderDatabase")
                               ?? "Server=localhost,1433;Database=GanjjOrder;User Id=sa;Password=Your_password123!;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new OrderDbContext(options);
    }
}
