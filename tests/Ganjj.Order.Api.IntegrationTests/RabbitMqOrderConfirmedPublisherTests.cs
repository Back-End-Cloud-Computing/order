using System.Text;
using Ganjj.Order.Infrastructure.Configuration;
using Ganjj.Order.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class RabbitMqOrderConfirmedPublisherTests : IAsyncLifetime
{
    private const string UserName = "order-test";
    private const string Password = "order-test-password";
    private readonly RabbitMqContainer rabbitMq = new RabbitMqBuilder("rabbitmq:4.1-management-alpine")
        .WithUsername(UserName)
        .WithPassword(Password)
        .Build();

    public Task InitializeAsync() => rabbitMq.StartAsync();

    public async Task DisposeAsync() => await rabbitMq.DisposeAsync();

    [Theory]
    [InlineData("OrderConfirmed", "order.confirmed")]
    [InlineData("OrderCreated", "order.created")]
    public async Task Publish_ShouldRoutePersistentEventWithContractProperties(string eventType, string routingKey)
    {
        const string exchange = "ganjj.orders";
        var factory = new ConnectionFactory
        {
            HostName = rabbitMq.Hostname,
            Port = rabbitMq.GetMappedPublicPort(5672),
            UserName = UserName,
            Password = Password
        };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: true, autoDelete: false);
        var declared = await channel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true,
            autoDelete: true);
        await channel.QueueBindAsync(declared.QueueName, exchange, routingKey);

        await using var publisher = new RabbitMqOrderConfirmedPublisher(Options.Create(new RabbitMqOptions
        {
            Enabled = true,
            HostName = rabbitMq.Hostname,
            Port = rabbitMq.GetMappedPublicPort(5672),
            UserName = UserName,
            Password = Password,
            Exchange = exchange,
            RoutingKey = "order.confirmed"
        }));
        var eventId = Guid.NewGuid();
        var payload = $"{{\"event_type\":\"{eventType}\"}}";

        await publisher.PublishAsync(
            new OutboxEnvelope(eventId, eventType, payload, "correlation-123", 1, Guid.NewGuid()),
            CancellationToken.None);

        var delivery = await channel.BasicGetAsync(declared.QueueName, autoAck: true);
        Assert.NotNull(delivery);
        Assert.Equal(payload, Encoding.UTF8.GetString(delivery.Body.Span));
        Assert.Equal(eventId.ToString("D"), delivery.BasicProperties.MessageId);
        Assert.Equal(eventType, delivery.BasicProperties.Type);
        Assert.Equal("correlation-123", delivery.BasicProperties.CorrelationId);
        Assert.Equal("application/json", delivery.BasicProperties.ContentType);
        Assert.True(delivery.BasicProperties.Persistent);
    }
}
