using System.Net;
using System.Text;
using System.Text.Json;
using Ganjj.Order.Application.Checkout;
using Ganjj.Order.Infrastructure.Configuration;
using Ganjj.Order.Infrastructure.Http;
using Microsoft.Extensions.Options;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class CartAndProductClientTests
{
    [Fact]
    public async Task CartClient_WhenCartContractIsValid_ShouldMapPromotionalSnapshot()
    {
        const string payload = """
                               {
                                 "items": [
                                   {
                                     "id": "cart-item-1",
                                     "product_id": "product-abc",
                                     "name": "Camiseta GANJJ",
                                     "price": 100.00,
                                     "color": "Preto",
                                     "image_url": "https://example.test/tshirt.png",
                                     "quantity": 2,
                                     "discount_percentage": 10,
                                     "discounted_price": 90.00
                                   }
                                 ],
                                 "subtotal": 200.00,
                                 "promotions_discount": 20.00,
                                 "coupon_discount": 0,
                                 "total": 180.00,
                                 "coupon": null,
                                 "discounts_applied": true
                               }
                               """;
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://shopping-cart:3003") };
        var client = new CartClient(httpClient);

        var cart = await client.GetCurrentAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "customer-token",
            "correlation-123",
            CancellationToken.None);

        Assert.Equal("http://shopping-cart:3003/api/cart", handler.RequestUri?.ToString());
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("customer-token", handler.AuthorizationParameter);
        Assert.Equal("correlation-123", handler.CorrelationId);
        var item = Assert.Single(cart.Items);
        Assert.Equal("product-abc", item.ProductId);
        Assert.Equal("Camiseta GANJJ", item.ProductName);
        Assert.Equal("Preto", item.Color);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(100m, item.UnitListPrice);
        Assert.Equal(90m, item.UnitFinalPrice);
    }

    [Fact]
    public async Task CartClient_WhenDiscountedPriceIsMissing_ShouldRejectPayload()
    {
        const string payload = """
                               {
                                 "items": [
                                   {
                                     "product_id": "product-abc",
                                     "name": "Camiseta GANJJ",
                                     "price": 100.00,
                                     "quantity": 2
                                   }
                                 ]
                               }
                               """;
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://shopping-cart:3003") };
        var client = new CartClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidDependencyContractException>(() =>
            client.GetCurrentAsync(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                "customer-token",
                "correlation-123",
                CancellationToken.None));

        Assert.Equal("invalid_dependency_contract", exception.Code);
        Assert.Contains("JSON payload is invalid", exception.Message);
    }

    [Fact]
    public async Task ProductStockClient_WhenDecrementing_ShouldSendProductContract()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://product-api:8000") };
        var client = new ProductStockClient(
            httpClient,
            Options.Create(new InternalServiceOptions { Secret = "shared-secret" }));

        await client.DecrementAsync(
            "product-abc",
            2,
            "operation-123",
            "correlation-123",
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "http://product-api:8000/internal/products/product-abc/stock/decrement",
            handler.RequestUri?.ToString());
        Assert.Equal("shared-secret", handler.InternalSecret);
        Assert.Equal("operation-123", handler.IdempotencyKey);
        Assert.Equal("correlation-123", handler.CorrelationId);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(2, body.RootElement.GetProperty("quantity").GetInt32());
        Assert.Equal("order_checkout", body.RootElement.GetProperty("reason").GetString());
        Assert.Equal("operation-123", body.RootElement.GetProperty("reference_id").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("compensates_operation_id").ValueKind);
    }

    [Fact]
    public async Task ProductStockClient_WhenRestoring_ShouldReferenceCompensatedOperation()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://product-api:8000") };
        var client = new ProductStockClient(
            httpClient,
            Options.Create(new InternalServiceOptions { Secret = "shared-secret" }));

        await client.RestoreAsync(
            "product-abc",
            2,
            "restore-123",
            "decrement-123",
            "correlation-123",
            CancellationToken.None);

        Assert.Equal(
            "http://product-api:8000/internal/products/product-abc/stock/restore",
            handler.RequestUri?.ToString());
        Assert.Equal("restore-123", handler.IdempotencyKey);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("order_compensation", body.RootElement.GetProperty("reason").GetString());
        Assert.Equal("restore-123", body.RootElement.GetProperty("reference_id").GetString());
        Assert.Equal("decrement-123", body.RootElement.GetProperty("compensates_operation_id").GetString());
    }

    [Fact]
    public async Task ProductStockClient_WhenProductReportsInsufficientStock_ShouldMapAvailableStock()
    {
        const string payload =
            """{"detail":"Product has only 1 unit in stock","error_type":"insufficient_stock","available_stock":1}""";
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://product-api:8000") };
        var client = new ProductStockClient(
            httpClient,
            Options.Create(new InternalServiceOptions { Secret = "shared-secret" }));

        var exception = await Assert.ThrowsAsync<Ganjj.Order.Application.Checkout.InsufficientStockException>(() =>
            client.DecrementAsync("product-abc", 2, "operation-123", "correlation-123", CancellationToken.None));

        Assert.Equal("product-abc", exception.ProductId);
        Assert.Equal(1, exception.AvailableStock);
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public string? InternalSecret { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            InternalSecret = request.Headers.TryGetValues("x-internal-secret", out var internalSecrets)
                ? internalSecrets.Single()
                : null;
            IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var idempotencyKeys)
                ? idempotencyKeys.Single()
                : null;
            CorrelationId = request.Headers.TryGetValues("X-Correlation-ID", out var correlationIds)
                ? correlationIds.Single()
                : null;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
