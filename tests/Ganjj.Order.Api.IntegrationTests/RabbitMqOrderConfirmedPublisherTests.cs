using System.Text;
using Ganjj.Order.Infrastructure.Configuration;
using Ganjj.Order.Infrastructure.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class RabbitMqOrderConfirmedPublisherTests : IAsyncLifetime
{
    private readonly RabbitMqContainer rabbitMq = new RabbitMqBuilder("rabbitmq:4.1-management-alpine").Build();

    public Task InitializeAsync() => rabbitMq.StartAsync();

    public async Task DisposeAsync() => await rabbitMq.DisposeAsync();

    [Fact]
    public async Task Publish_ShouldDeliverPersistentOrderConfirmedWithContractProperties()
    {
        const string exchange = "ganjj.orders";
        const string routingKey = "order.confirmed";
        var factory = new ConnectionFactory
        {
            HostName = rabbitMq.Hostname,
            Port = rabbitMq.GetMappedPublicPort(5672),
            UserName = "guest",
            Password = "guest"
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
            UserName = "guest",
            Password = "guest",
            Exchange = exchange,
            RoutingKey = routingKey
        }));
        var eventId = Guid.NewGuid();
        const string payload = "{\"schema_version\":1,\"event_type\":\"OrderConfirmed\"}";

        await publisher.PublishAsync(
            new OutboxEnvelope(eventId, "OrderConfirmed", payload, "correlation-123", 1, Guid.NewGuid()),
            CancellationToken.None);

        var delivery = await channel.BasicGetAsync(declared.QueueName, autoAck: true);
        Assert.NotNull(delivery);
        Assert.Equal(payload, Encoding.UTF8.GetString(delivery.Body.Span));
        Assert.Equal(eventId.ToString("D"), delivery.BasicProperties.MessageId);
        Assert.Equal("OrderConfirmed", delivery.BasicProperties.Type);
        Assert.Equal("correlation-123", delivery.BasicProperties.CorrelationId);
        Assert.Equal("application/json", delivery.BasicProperties.ContentType);
        Assert.True(delivery.BasicProperties.Persistent);
    }
}
