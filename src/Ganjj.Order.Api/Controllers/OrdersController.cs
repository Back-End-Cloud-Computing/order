using System.Globalization;
using System.Security.Claims;
using Ganjj.Order.Api.Authentication;
using Ganjj.Order.Api.Contracts;
using Ganjj.Order.Api.Middleware;
using Ganjj.Order.Application.Checkout;
using Ganjj.Order.Application.Orders;
using Ganjj.Order.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ganjj.Order.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/pedidos")]
public sealed class OrdersController(
    CreateOrderHandler createOrderHandler,
    OrderManagementService orderManagementService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateOrderResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateOrderResponse>> Create(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var customerId = CustomerId();
        var bearerToken = BearerToken();
        var address = request.DeliveryAddress;
        var result = await createOrderHandler.HandleAsync(new CreateOrderCommand(
            customerId,
            User.FindFirstValue("name"),
            User.FindFirstValue("email"),
            bearerToken,
            idempotencyKey ?? string.Empty,
            new DeliveryAddress(
                address.Street,
                address.Number,
                address.Complement,
                address.District,
                address.City,
                address.State,
                address.PostalCode,
                address.Country),
            HttpContext.Items[CorrelationIdMiddleware.ItemName]?.ToString()), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result.ToResponse());
    }

    [HttpGet("meus")]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> ListMine(CancellationToken cancellationToken)
    {
        var orders = await orderManagementService.ListMineAsync(CustomerId(), cancellationToken);
        return Ok(orders.Select(OrderResponseMapper.ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderManagementService.GetAsync(id, CustomerId(), IsAdmin(), cancellationToken);
        return Ok(order.ToResponse());
    }

    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [HttpGet("admin/todos")]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> ListAll(CancellationToken cancellationToken)
    {
        var orders = await orderManagementService.ListAllAsync(cancellationToken);
        return Ok(orders.Select(OrderResponseMapper.ToResponse).ToList());
    }

    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [HttpPut("{id:guid}/passo")]
    public async Task<ActionResult<OrderResponse>> UpdateTracking(
        Guid id,
        [FromBody] UpdateTrackingRequest request,
        CancellationToken cancellationToken)
    {
        var order = await orderManagementService.UpdateTrackingAsync(
            id, request.Step, request.TrackingNumber, cancellationToken);
        return Ok(order.ToResponse());
    }

    private long CustomerId()
    {
        var raw = User.FindFirstValue("id");
        if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            throw new DomainValidationException("The token must contain a positive numeric id claim.");
        }

        return id;
    }

    private bool IsAdmin() => string.Equals(
        User.FindFirstValue("isAdmin"), "true", StringComparison.OrdinalIgnoreCase);

    private string BearerToken()
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        const string prefix = "Bearer ";
        if (authorization?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) is true)
        {
            return authorization[prefix.Length..].Trim();
        }

        if (Request.Cookies.TryGetValue("accessToken", out var token) && !string.IsNullOrWhiteSpace(token))
        {
            return token;
        }

        throw new DomainValidationException("A bearer token is required for the current Cart integration.");
    }
}
