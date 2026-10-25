using Ganjj.Order.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdIsProvided_ShouldEchoItAndAddItToTheLoggingScope()
    {
        const string correlationId = "6a2f41a5-c3f9-4d0f-a8cb-0a6e8a8e1a11";
        var logger = new ScopeRecordingLogger<CorrelationIdMiddleware>();
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        var scopeWasVisibleToThePipeline = false;
        RequestDelegate next = _ =>
        {
            scopeWasVisibleToThePipeline = logger.ActiveCorrelationId == correlationId;
            return Task.CompletedTask;
        };
        var middleware = new CorrelationIdMiddleware(next, logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.HeaderName]);
        Assert.True(scopeWasVisibleToThePipeline);
    }

    private sealed class ScopeRecordingLogger<T> : ILogger<T>
    {
        public string? ActiveCorrelationId { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var previous = ActiveCorrelationId;
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                ActiveCorrelationId = values
                    .FirstOrDefault(pair => pair.Key == CorrelationIdMiddleware.ItemName)
                    .Value?.ToString();
            }

            return new CallbackDisposable(() => ActiveCorrelationId = previous);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class CallbackDisposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}
