using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Ganjj.Order.Api.Contracts;

public sealed record CreateOrderRequest(
    [property: Required, JsonPropertyName("endereco_entrega")] DeliveryAddressRequest DeliveryAddress);

public sealed record DeliveryAddressRequest(
    [property: Required, JsonPropertyName("rua")] string Street,
    [property: Required, JsonPropertyName("numero")] string Number,
    [property: JsonPropertyName("complemento")] string? Complement,
    [property: Required, JsonPropertyName("bairro")] string District,
    [property: Required, JsonPropertyName("cidade")] string City,
    [property: Required, JsonPropertyName("estado")] string State,
    [property: Required, JsonPropertyName("cep")] string PostalCode,
    [property: Required, JsonPropertyName("pais")] string Country);

public sealed record UpdateTrackingRequest(
    [property: Range(0, 5), JsonPropertyName("passo")] int Step,
    [property: JsonPropertyName("numero_rastreio")] string? TrackingNumber);

public sealed record CreateOrderResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("codigo")] string Code,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("passo")] int TrackingStep,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("criado_em")] DateTimeOffset CreatedAt);

public sealed record OrderResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("codigo")] string Code,
    [property: JsonPropertyName("cliente_id")] Guid CustomerId,
    [property: JsonPropertyName("cliente_nome")] string? CustomerName,
    [property: JsonPropertyName("cliente_email")] string? CustomerEmail,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("passo")] int TrackingStep,
    [property: JsonPropertyName("numero_rastreio")] string? TrackingNumber,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("endereco_entrega")] DeliveryAddressResponse DeliveryAddress,
    [property: JsonPropertyName("itens")] IReadOnlyList<OrderItemResponse> Items,
    [property: JsonPropertyName("criado_em")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("atualizado_em")] DateTimeOffset UpdatedAt);

public sealed record DeliveryAddressResponse(
    [property: JsonPropertyName("rua")] string Street,
    [property: JsonPropertyName("numero")] string Number,
    [property: JsonPropertyName("complemento")] string? Complement,
    [property: JsonPropertyName("bairro")] string District,
    [property: JsonPropertyName("cidade")] string City,
    [property: JsonPropertyName("estado")] string State,
    [property: JsonPropertyName("cep")] string PostalCode,
    [property: JsonPropertyName("pais")] string Country);

public sealed record OrderItemResponse(
    [property: JsonPropertyName("produto_id")] long ProductId,
    [property: JsonPropertyName("nome")] string ProductName,
    [property: JsonPropertyName("tamanho")] string? Size,
    [property: JsonPropertyName("imagem_url")] string? ImageUrl,
    [property: JsonPropertyName("quantidade")] int Quantity,
    [property: JsonPropertyName("preco_unitario")] decimal UnitFinalPrice,
    [property: JsonPropertyName("subtotal")] decimal LineTotal);
