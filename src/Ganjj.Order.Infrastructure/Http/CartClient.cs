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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
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
                    throw new InvalidDependencyContractException("Cart", "the items collection is missing");
                }

                return new CartSnapshot(payload.Items.Select(item => new CartItemSnapshot(
                    item.ProductId,
                    item.Name,
                    item.Color,
                    item.ImageUrl,
                    item.Quantity,
                    item.Price,
                    item.DiscountedPrice)).ToList());
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

    private sealed record CartResponse([property: JsonPropertyName("items")] List<CartItemResponse> Items);

    private sealed record CartItemResponse(
        [property: JsonPropertyName("product_id")] string ProductId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("color")] string? Color,
        [property: JsonPropertyName("image_url")] string? ImageUrl,
        [property: JsonPropertyName("quantity")] int Quantity,
        [property: JsonPropertyName("price"), JsonRequired] decimal Price,
        [property: JsonPropertyName("discounted_price"), JsonRequired] decimal DiscountedPrice);
}
