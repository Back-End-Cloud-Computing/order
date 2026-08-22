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
        long productId,
        int quantity,
        string operationId,
        string correlationId,
        CancellationToken cancellationToken) =>
        SendAsync(productId, quantity, "decrement-stock", operationId, null, correlationId, cancellationToken);

    public Task RestoreAsync(
        long productId,
        int quantity,
        string operationId,
        string compensatesOperationId,
        string correlationId,
        CancellationToken cancellationToken) =>
        SendAsync(productId, quantity, "restore-stock", operationId, compensatesOperationId, correlationId,
            cancellationToken);

    private async Task SendAsync(
        long productId,
        int quantity,
        string action,
        string operationId,
        string? compensatesOperationId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/internal/produtos/{productId}/{action}");
        request.Headers.TryAddWithoutValidation("x-internal-secret", internalOptions.Value.Secret);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        request.Content = JsonContent.Create(new StockRequest(quantity, operationId, compensatesOperationId));

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

            if (action == "decrement-stock" && response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
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
        [property: JsonPropertyName("quantidade")] int Quantity,
        [property: JsonPropertyName("operation_id")] string OperationId,
        [property: JsonPropertyName("compensates_operation_id")] string? CompensatesOperationId);

    private sealed record ProductError(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("estoque_disponivel")] int? AvailableStock);
}
