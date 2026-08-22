using Ganjj.Order.Application.Checkout;
using Ganjj.Order.Domain.Orders;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ganjj.Order.Api.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            DomainValidationException => (StatusCodes.Status400BadRequest, "Invalid request", "validation_error"),
            OrderForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden", "order_forbidden"),
            OrderNotFoundException => (StatusCodes.Status404NotFound, "Order not found", "order_not_found"),
            ProductNotFoundException => (StatusCodes.Status404NotFound, "Product not found", "product_not_found"),
            CartEmptyException => (StatusCodes.Status409Conflict, "Cart is empty", "cart_empty"),
            IdempotencyConflictException => (StatusCodes.Status409Conflict, "Idempotency conflict", "idempotency_conflict"),
            InsufficientStockException => (StatusCodes.Status409Conflict, "Insufficient stock", "insufficient_stock"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrent update", "concurrency_conflict"),
            InvalidDependencyContractException => (StatusCodes.Status502BadGateway, "Invalid dependency response", "invalid_dependency_contract"),
            DependencyUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Dependency unavailable", "dependency_unavailable"),
            CompensationFailedException => (StatusCodes.Status500InternalServerError, "Checkout compensation failed", "compensation_failed"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error", "unexpected_error")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Request failed with {ErrorCode}", code);
        }
        else
        {
            logger.LogWarning(exception, "Request rejected with {ErrorCode}", code);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "The request could not be completed."
                    : exception.Message,
                Extensions =
                {
                    ["code"] = code,
                    ["traceId"] = httpContext.TraceIdentifier
                }
            }
        });
    }
}
