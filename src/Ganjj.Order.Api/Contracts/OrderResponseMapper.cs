using Ganjj.Order.Application.Checkout;
using OrderAggregate = Ganjj.Order.Domain.Orders.Order;

namespace Ganjj.Order.Api.Contracts;

public static class OrderResponseMapper
{
    public static CreateOrderResponse ToResponse(this CreateOrderResult result) =>
        new(result.Id, result.Code, result.Status.ToString(), result.TrackingStep, result.Total, result.CreatedAt);

    public static OrderResponse ToResponse(this OrderAggregate order) =>
        new(
            order.Id,
            order.Code,
            order.CustomerId,
            order.CustomerName,
            order.CustomerEmail,
            order.Status.ToString(),
            order.TrackingStep,
            order.TrackingNumber,
            order.Total,
            new DeliveryAddressResponse(
                order.DeliveryAddress.Street,
                order.DeliveryAddress.Number,
                order.DeliveryAddress.Complement,
                order.DeliveryAddress.District,
                order.DeliveryAddress.City,
                order.DeliveryAddress.State,
                order.DeliveryAddress.PostalCode,
                order.DeliveryAddress.Country),
            order.Items.Select(item => new OrderItemResponse(
                item.ProductId,
                item.ProductName,
                item.Size,
                item.ImageUrl,
                item.Quantity,
                item.UnitFinalPrice,
                item.LineTotal)).ToList(),
            order.CreatedAt,
            order.UpdatedAt);
}
