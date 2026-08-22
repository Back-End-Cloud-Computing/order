using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class ApiFoundationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiFoundationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"))
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
}
