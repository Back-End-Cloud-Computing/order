using Scalar.AspNetCore;
using Ganjj.Order.Api.Authentication;
using Ganjj.Order.Api.Errors;
using Ganjj.Order.Api.Middleware;
using Ganjj.Order.Application.Checkout;
using Ganjj.Order.Application.Orders;
using Ganjj.Order.Infrastructure;
using Ganjj.Order.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOrderAuthentication(builder.Configuration);
builder.Services.AddOrderInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CreateOrderHandler>();
builder.Services.AddScoped<OrderManagementService>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

var apiDocsEnabled = app.Environment.IsDevelopment() ||
                     app.Configuration.GetValue<bool>("ApiDocs:Enabled");

if (apiDocsEnabled)
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("GANJJ Order Service API"));
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithName("Health")
    .WithTags("Health");
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithName("LiveHealth")
    .WithTags("Health");
app.MapGet("/health/ready", async (OrderDbContext dbContext, CancellationToken cancellationToken) =>
        await dbContext.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "healthy" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous()
    .WithName("ReadyHealth")
    .WithTags("Health");
app.MapGet("/instance", () => Results.Ok(new
    {
        pod = Environment.GetEnvironmentVariable("HOSTNAME") ?? Environment.MachineName
    }))
    .AllowAnonymous()
    .WithName("GetInstance")
    .WithTags("Diagnostics");

app.Run();

public partial class Program;
