using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Ganjj.Order.Infrastructure.Http;

internal sealed class SupportNotificationClient(
    HttpClient httpClient,
    IOptions<InternalServiceOptions> internalOptions) : IOrderCreatedNotifier
{
    public async Task NotifyAsync(
        OrderCreatedNotification notification,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/notifications");
        request.Headers.TryAddWithoutValidation("x-internal-secret", internalOptions.Value.Secret);
        if (!string.IsNullOrWhiteSpace(notification.CorrelationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", notification.CorrelationId);
        }

        request.Content = JsonContent.Create(new SupportOrderCreatedRequest(
            notification.EventId,
            "OrderCreated",
            notification.OccurredAt,
            notification.OrderId.ToString(),
            notification.OrderCode,
            notification.CustomerId.ToString(),
            notification.CustomerEmail,
            notification.CustomerPhone,
            notification.Total,
            notification.Currency,
            notification.CorrelationId));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record SupportOrderCreatedRequest(
        [property: JsonPropertyName("event_id")] Guid EventId,
        [property: JsonPropertyName("event_type")] string EventType,
        [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt,
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("order_code")] string OrderCode,
        [property: JsonPropertyName("customer_id")] string CustomerId,
        [property: JsonPropertyName("customer_email")] string CustomerEmail,
        [property: JsonPropertyName("customer_phone"),
         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CustomerPhone,
        [property: JsonPropertyName("total")] decimal Total,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("correlation_id"),
         JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CorrelationId);
}
