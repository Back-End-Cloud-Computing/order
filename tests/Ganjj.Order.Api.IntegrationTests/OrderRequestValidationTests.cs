using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Application.Checkout;
using Ganjj.Order.Domain.Orders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class OrderRequestValidationTests : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _publicKeyPath = Path.Combine(
        Path.GetTempPath(), $"ganjj-order-{Guid.NewGuid():N}.pem");

    public OrderRequestValidationTests() =>
        File.WriteAllText(_publicKeyPath, _rsa.ExportSubjectPublicKeyInfoPem());

    [Fact]
    public async Task CreateOrder_WhenRequestIsValid_ShouldReturnCreated()
    {
        var repository = new SuccessfulOrderRepository();
        var notifier = new RecordingOrderCreatedNotifier();
        using var factory = Factory(repository, notifier);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        client.DefaultRequestHeaders.Add("Idempotency-Key", "checkout-valid-request");

        var response = await client.PostAsJsonAsync("/api/pedidos", new
        {
            endereco_entrega = new
            {
                rua = "Rua das Flores",
                numero = "123",
                complemento = "Apto 4",
                bairro = "Centro",
                cidade = "Curitiba",
                estado = "PR",
                cep = "80000-000",
                pais = "BR"
            }
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created, received {(int)response.StatusCode}: {responseBody}");
        Assert.NotNull(response.Headers.Location);

        var listResponse = await client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var orders = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        Assert.Single(orders.RootElement.EnumerateArray());
        var notification = Assert.Single(notifier.Notifications);
        Assert.Equal("customer@ganjj.test", notification.CustomerEmail);
    }

    [Fact]
    public async Task UpdateTracking_WhenStepIsOutsideRange_ShouldReturnBadRequest()
    {
        using var factory = Factory(new SuccessfulOrderRepository());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("ADMIN"));

        var response = await client.PutAsJsonAsync($"/api/pedidos/{Guid.NewGuid():D}/passo", new
        {
            passo = 6,
            numero_rastreio = "BR123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private WebApplicationFactory<Program> Factory(
        SuccessfulOrderRepository repository,
        RecordingOrderCreatedNotifier? notifier = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Jwt:PublicKeyPath", _publicKeyPath);
            builder.UseSetting("Authentication:Jwt:Issuer", "ganjj-authorization");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IOrderRepository>(repository);
                services.AddScoped<ICartClient, ValidCartClient>();
                services.AddScoped<IProductStockClient, SuccessfulProductStockClient>();
                services.AddSingleton<IIdentifierGenerator, TestIdentifierGenerator>();
                services.AddSingleton<IOrderCreatedNotifier>(notifier ?? new RecordingOrderCreatedNotifier());
            });
        });

    private string Token(string role = "CLIENTE")
    {
        var credentials = new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "ganjj-authorization",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "11111111-1111-1111-1111-111111111111"),
                new Claim(JwtRegisteredClaimNames.Email, "customer@ganjj.test"),
                new Claim("role", role),
                new Claim("typ", "access")
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        _rsa.Dispose();
        if (File.Exists(_publicKeyPath)) File.Delete(_publicKeyPath);
    }

    private sealed class ValidCartClient : ICartClient
    {
        public Task<CartSnapshot> GetCurrentAsync(
            Guid customerId,
            string bearerToken,
            string correlationId,
            CancellationToken cancellationToken) => Task.FromResult(new CartSnapshot(
            [new CartItemSnapshot(101, "Camiseta GANJJ", "M", null, 2, 49.90m)]));
    }

    private sealed class SuccessfulProductStockClient : IProductStockClient
    {
        public Task DecrementAsync(
            long productId,
            int quantity,
            string operationId,
            string correlationId,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RestoreAsync(
            long productId,
            int quantity,
            string operationId,
            string compensatesOperationId,
            string correlationId,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestIdentifierGenerator : IIdentifierGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private sealed class RecordingOrderCreatedNotifier : IOrderCreatedNotifier
    {
        public List<OrderCreatedNotification> Notifications { get; } = [];

        public Task NotifyAsync(
            OrderCreatedNotification notification,
            CancellationToken cancellationToken)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class SuccessfulOrderRepository : IOrderRepository
    {
        private OrderAggregate? _order;

        public Task<IdempotencySnapshot?> FindIdempotencyAsync(
            string key,
            CancellationToken cancellationToken) => Task.FromResult<IdempotencySnapshot?>(null);

        public Task<bool> TryStartAsync(
            string key,
            string requestHash,
            Guid customerId,
            DateTimeOffset now,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task SaveAndCompleteAsync(
            OrderAggregate order,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            _order = order;
            return Task.CompletedTask;
        }

        public Task RemoveIdempotencyAsync(
            string key,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<OrderAggregate?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<OrderAggregate?>(null);

        public Task<IReadOnlyList<OrderAggregate>> ListForCustomerAsync(
            Guid customerId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderAggregate>>(
                _order is not null && _order.CustomerId == customerId ? [_order] : []);

        public Task<IReadOnlyList<OrderAggregate>> ListAllAsync(
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderAggregate>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
