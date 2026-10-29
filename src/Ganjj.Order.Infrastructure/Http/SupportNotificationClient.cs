using System.Text;
using Ganjj.Order.Infrastructure.Messaging;
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

        request.Content = new StringContent(
            OrderCreatedSerializer.Serialize(notification), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

}
