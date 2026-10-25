using System.Net;
using System.Text.Json;
using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Infrastructure.Configuration;
using Ganjj.Order.Infrastructure.Http;
using Microsoft.Extensions.Options;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class SupportNotificationClientTests
{
    [Fact]
    public async Task NotifyAsync_WhenEventIsValid_ShouldSendSupportContractAndInternalHeaders()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://support:8090")
        };
        var client = new SupportNotificationClient(
            httpClient,
            Options.Create(new InternalServiceOptions { Secret = "shared-secret" }));
        var notification = new OrderCreatedNotification(
            Guid.Parse("0b6f3a3e-8d0e-4c34-9d36-4f0f2f5b9a11"),
            new DateTimeOffset(2026, 10, 19, 14, 30, 0, TimeSpan.Zero),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "#GNJ-20261019-AAAA",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "cliente@example.com",
            null,
            1234.56m,
            "BRL",
            "6a2f41a5-c3f9-4d0f-a8cb-0a6e8a8e1a11");

        await client.NotifyAsync(notification, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("http://support:8090/internal/notifications", handler.RequestUri?.ToString());
        Assert.Equal("shared-secret", handler.InternalSecret);
        Assert.Equal(notification.CorrelationId, handler.CorrelationId);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(notification.EventId.ToString(), body.RootElement.GetProperty("event_id").GetString());
        Assert.Equal("OrderCreated", body.RootElement.GetProperty("event_type").GetString());
        Assert.Equal(notification.OccurredAt,
            body.RootElement.GetProperty("occurred_at").GetDateTimeOffset());
        Assert.Equal(notification.OrderId.ToString(), body.RootElement.GetProperty("order_id").GetString());
        Assert.Equal(notification.OrderCode, body.RootElement.GetProperty("order_code").GetString());
        Assert.Equal(notification.CustomerId.ToString(), body.RootElement.GetProperty("customer_id").GetString());
        Assert.Equal(notification.CustomerEmail, body.RootElement.GetProperty("customer_email").GetString());
        Assert.False(body.RootElement.TryGetProperty("customer_phone", out _));
        Assert.Equal(1234.56m, body.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal("BRL", body.RootElement.GetProperty("currency").GetString());
        Assert.Equal(notification.CorrelationId,
            body.RootElement.GetProperty("correlation_id").GetString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? InternalSecret { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            InternalSecret = request.Headers.GetValues("x-internal-secret").Single();
            CorrelationId = request.Headers.GetValues("X-Correlation-ID").Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }
}
