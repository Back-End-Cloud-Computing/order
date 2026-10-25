using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class ApiFoundationTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _publicKeyPath = Path.Combine(Path.GetTempPath(), $"ganjj-order-{Guid.NewGuid():N}.pem");

    public ApiFoundationTests()
    {
        using var rsa = RSA.Create(2048);
        File.WriteAllText(_publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem());
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Authentication:Jwt:PublicKeyPath", _publicKeyPath);
            builder.UseSetting("Authentication:Jwt:Issuer", "ganjj-authorization");
        });
        _client = _factory
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task LiveHealth_WhenApiIsRunning_ShouldReturnHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("healthy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_WhenApiIsRunning_ShouldReturnHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("healthy", document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Instance_WhenApiIsRunning_ShouldIdentifyTheServingProcess()
    {
        var response = await _client.GetAsync("/instance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("pod").GetString()));
    }

    [Fact]
    public async Task OpenApi_WhenDevelopment_ShouldExposeVersionOneDocument()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("3.1.1", document.RootElement.GetProperty("openapi").GetString());
    }

    [Fact]
    public async Task Scalar_WhenDevelopment_ShouldExposeInteractiveReference()
    {
        var response = await _client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("scalar", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Orders_WhenAnonymous_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync("/api/pedidos/meus");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        if (File.Exists(_publicKeyPath)) File.Delete(_publicKeyPath);
    }
}
