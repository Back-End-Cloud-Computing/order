using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Application.Checkout;
using Ganjj.Order.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Ganjj.Order.Infrastructure.Http;

internal sealed class ProductStockClient(
    HttpClient httpClient,
    IOptions<InternalServiceOptions> internalOptions) : IProductStockClient
{
    public Task DecrementAsync(
        string productId,
        int quantity,
        string operationId,
        string correlationId,
        CancellationToken cancellationToken) =>
        SendAsync(productId, quantity, "decrement", "order_checkout", operationId, null, correlationId,
            cancellationToken);

    public Task RestoreAsync(
        string productId,
        int quantity,
        string operationId,
        string compensatesOperationId,
        string correlationId,
        CancellationToken cancellationToken) =>
        SendAsync(productId, quantity, "restore", "order_compensation", operationId, compensatesOperationId,
            correlationId, cancellationToken);

    private async Task SendAsync(
        string productId,
        int quantity,
        string action,
        string reason,
        string operationId,
        string? compensatesOperationId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var escapedProductId = Uri.EscapeDataString(productId);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/internal/products/{escapedProductId}/stock/{action}");
        request.Headers.TryAddWithoutValidation("x-internal-secret", internalOptions.Value.Secret);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        request.Content = JsonContent.Create(new StockRequest(quantity, reason, operationId, compensatesOperationId));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new DependencyUnavailableException("Product", exception);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode) return;

            ProductError? error = null;
            try
            {
                error = await response.Content.ReadFromJsonAsync<ProductError>(cancellationToken);
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
            {
                // The status code still provides the safe public mapping.
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ProductNotFoundException(productId);
            }

            if (action == "decrement" && response.StatusCode == HttpStatusCode.Conflict &&
                string.Equals(error?.ErrorType, "insufficient_stock", StringComparison.Ordinal))
            {
                throw new InsufficientStockException(productId, error?.AvailableStock);
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new DependencyUnavailableException("Product");
            }

            throw new InvalidDependencyContractException(
                "Product", $"unexpected HTTP status {(int)response.StatusCode}");
        }
    }

    private sealed record StockRequest(
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("reason")] string Reason,
        [property: JsonPropertyName("reference_id")] string ReferenceId,
        [property: JsonPropertyName("compensates_operation_id")] string? CompensatesOperationId);

    private sealed record ProductError(
        [property: JsonPropertyName("error_type")] string? ErrorType,
        [property: JsonPropertyName("available_stock")] int? AvailableStock);
}
