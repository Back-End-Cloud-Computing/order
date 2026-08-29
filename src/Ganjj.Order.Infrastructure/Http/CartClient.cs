using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ganjj.Order.Application.Abstractions;
using Ganjj.Order.Application.Checkout;

namespace Ganjj.Order.Infrastructure.Http;

internal sealed class CartClient(HttpClient httpClient) : ICartClient
{
    public async Task<CartSnapshot> GetCurrentAsync(
        Guid customerId,
        string bearerToken,
        string correlationId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cesta");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new DependencyUnavailableException("Cart", exception);
        }

        using (response)
        {
            if ((int)response.StatusCode >= 500)
            {
                throw new DependencyUnavailableException("Cart");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidDependencyContractException(
                    "Cart", $"unexpected HTTP status {(int)response.StatusCode}");
            }

            try
            {
                var payload = await response.Content.ReadFromJsonAsync<CartResponse>(cancellationToken);
                if (payload?.Items is null)
                {
                    throw new InvalidDependencyContractException("Cart", "the itens collection is missing");
                }

                return new CartSnapshot(payload.Items.Select(item => new CartItemSnapshot(
                    item.ProductId,
                    item.Name,
                    item.Size,
                    item.ImageUrl,
                    item.Quantity,
                    item.Price)).ToList());
            }
            catch (InvalidDependencyContractException)
            {
                throw;
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                throw new InvalidDependencyContractException("Cart", "the JSON payload is invalid");
            }
        }
    }

    private sealed record CartResponse([property: JsonPropertyName("itens")] List<CartItemResponse> Items);

    private sealed record CartItemResponse(
        [property: JsonPropertyName("produto_id")] long ProductId,
        [property: JsonPropertyName("nome")] string Name,
        [property: JsonPropertyName("tamanho")] string? Size,
        [property: JsonPropertyName("imagem_url")] string? ImageUrl,
        [property: JsonPropertyName("quantidade")] int Quantity,
        [property: JsonPropertyName("preco")] decimal Price);
}
