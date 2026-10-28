using System.Text;
using Ganjj.Order.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Ganjj.Order.Infrastructure.Messaging;

internal sealed class RabbitMqOrderConfirmedPublisher : IOrderConfirmedPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions options;
    private readonly SemaphoreSlim mutex = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public RabbitMqOrderConfirmedPublisher(IOptions<RabbitMqOptions> options)
    {
        this.options = options.Value;
    }

    public async Task PublishAsync(OutboxEnvelope message, CancellationToken cancellationToken)
    {
        await mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Type = message.EventType,
                MessageId = message.Id.ToString("D"),
                CorrelationId = message.CorrelationId
            };
            await channel!.BasicPublishAsync(
                options.Exchange,
                options.RoutingKey,
                mandatory: true,
                properties,
                Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally
        {
            mutex.Release();
        }
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (channel is { IsOpen: true }) return;

        await ResetAsync();
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProvidedName = "ganjj-order-outbox"
        };
        connection = await factory.CreateConnectionAsync(cancellationToken);
        channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true), cancellationToken);
        await channel.ExchangeDeclareAsync(
            options.Exchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
    }

    private async Task ResetAsync()
    {
        if (channel is not null)
        {
            await channel.DisposeAsync();
            channel = null;
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
            connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await mutex.WaitAsync();
        try
        {
            await ResetAsync();
        }
        finally
        {
            mutex.Release();
            mutex.Dispose();
        }
    }
}
