using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
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

public sealed class JwtAuthenticationTests : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly string _publicKeyPath = Path.Combine(Path.GetTempPath(), $"ganjj-order-{Guid.NewGuid():N}.pem");

    public JwtAuthenticationTests() => File.WriteAllText(_publicKeyPath, _rsa.ExportSubjectPublicKeyInfoPem());

    [Fact]
    public async Task CustomerRoute_WhenAccessTokenMatchesAuthorizationContract_ShouldAuthenticateSubUuid()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());

        var response = await client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminRoute_WhenRoleIsCliente_ShouldReturnForbidden()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role: "CLIENTE"));

        var response = await client.GetAsync("/api/pedidos/admin/todos");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminRoute_WhenRoleIsAdmin_ShouldReturnSuccess()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role: "ADMIN"));

        var response = await client.GetAsync("/api/pedidos/admin/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("refresh", "ganjj-authorization")]
    [InlineData("access", "another-issuer")]
    public async Task ProtectedRoute_WhenTokenContractIsInvalid_ShouldReturnUnauthorized(string type, string issuer)
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", Token(type: type, issuer: issuer));

        var response = await client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WhenSubIsNotUuid_ShouldReturnUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(subject: "42"));

        var response = await client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WhenEmailClaimIsMissing_ShouldReturnUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", Token(includeEmail: false));

        var response = await client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WhenTokenIsExpired_ShouldReturnUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", Token(notBefore: DateTime.UtcNow.AddMinutes(-10), expires: DateTime.UtcNow.AddMinutes(-5)));

        var response = await client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WhenTokenIsTampered_ShouldReturnUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var token = Token();
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedRoute_WhenTokenUsesHs256_ShouldReturnUnauthorized()
    {
        using var factory = Factory();
        using var client = factory.CreateClient();
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey("a-test-key-that-is-long-enough-for-hs256"u8.ToArray()),
            SecurityAlgorithms.HmacSha256);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", Token(credentials: credentials));

        var response = await client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Startup_WhenPublicKeyDoesNotExist_ShouldFailClosed()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Jwt:PublicKeyPath", Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pem"));
            builder.UseSetting("Authentication:Jwt:Issuer", "ganjj-authorization");
        });

        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }

    private WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Jwt:PublicKeyPath", _publicKeyPath);
            builder.UseSetting("Authentication:Jwt:Issuer", "ganjj-authorization");
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IOrderRepository, EmptyOrderRepository>();
            });
        });

    private string Token(
        string role = "CLIENTE",
        string type = "access",
        string issuer = "ganjj-authorization",
        string subject = "11111111-1111-1111-1111-111111111111",
        bool includeEmail = true,
        DateTime? notBefore = null,
        DateTime? expires = null,
        SigningCredentials? credentials = null)
    {
        credentials ??= new SigningCredentials(new RsaSecurityKey(_rsa), SecurityAlgorithms.RsaSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new("role", role),
            new("typ", type)
        };
        if (includeEmail)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, "customer@ganjj.test"));
        }

        var token = new JwtSecurityToken(
            issuer,
            claims: claims,
            notBefore: notBefore ?? DateTime.UtcNow.AddMinutes(-1),
            expires: expires ?? DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        _rsa.Dispose();
        if (File.Exists(_publicKeyPath)) File.Delete(_publicKeyPath);
    }

    private sealed class EmptyOrderRepository : IOrderRepository
    {
        public Task<IdempotencySnapshot?> FindIdempotencyAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<IdempotencySnapshot?>(null);

        public Task<bool> TryStartAsync(string key, string requestHash, Guid customerId, DateTimeOffset now,
            CancellationToken cancellationToken) => Task.FromResult(true);

        public Task SaveAndCompleteAsync(OrderAggregate order, OrderConfirmedEvent orderConfirmedEvent,
            string idempotencyKey,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveIdempotencyAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<OrderAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<OrderAggregate?>(null);

        public Task<IReadOnlyList<OrderAggregate>> ListForCustomerAsync(Guid customerId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OrderAggregate>>([]);

        public Task<IReadOnlyList<OrderAggregate>> ListAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OrderAggregate>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
