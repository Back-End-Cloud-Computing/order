using Ganjj.Order.Infrastructure.Configuration;
using Ganjj.Order.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ganjj.Order.Api.IntegrationTests;

public sealed class OutboxProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Process_WhenPublishSucceeds_ShouldMarkMessageAsPublished()
    {
        var message = Message(attemptCount: 1);
        var store = new FakeStore(message);
        var publisher = new FakePublisher();
        var processor = Create(store, publisher);

        var processed = await processor.ProcessAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Same(message, Assert.Single(publisher.Messages));
        Assert.Same(message, Assert.Single(store.Published));
        Assert.Empty(store.Failed);
    }

    [Fact]
    public async Task Process_WhenPublishFails_ShouldReleaseMessageWithExponentialRetry()
    {
        var message = Message(attemptCount: 3);
        var store = new FakeStore(message);
        var publisher = new FakePublisher { Failure = new InvalidOperationException("broker unavailable") };
        var processor = Create(store, publisher);

        var processed = await processor.ProcessAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        var failed = Assert.Single(store.Failed);
        Assert.Same(message, failed.Message);
        Assert.Equal(Now.AddSeconds(8), failed.NextAttemptAt);
        Assert.Equal("broker unavailable", failed.Error);
        Assert.Empty(store.Published);
    }

    [Fact]
    public async Task Process_WhenLeaseWasReclaimed_ShouldNotPublishOrChangeMessage()
    {
        var message = Message(attemptCount: 1);
        var store = new FakeStore(message) { OwnsLease = false };
        var publisher = new FakePublisher();
        var processor = Create(store, publisher);

        var processed = await processor.ProcessAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Empty(publisher.Messages);
        Assert.Empty(store.Published);
        Assert.Empty(store.Failed);
    }

    private static OutboxProcessor Create(FakeStore store, FakePublisher publisher) =>
        new(store, publisher, new FixedTimeProvider(), Options.Create(new RabbitMqOptions
        {
            LeaseSeconds = 30,
            BatchSize = 20
        }), NullLogger<OutboxProcessor>.Instance);

    private static OutboxEnvelope Message(int attemptCount) =>
        new(Guid.NewGuid(), "OrderConfirmed", "{}", "correlation", attemptCount, Guid.NewGuid());

    private sealed class FakeStore(params OutboxEnvelope[] messages) : IOutboxStore
    {
        public bool OwnsLease { get; init; } = true;
        public List<OutboxEnvelope> Published { get; } = [];
        public List<FailedMessage> Failed { get; } = [];

        public Task<IReadOnlyList<OutboxEnvelope>> ClaimAsync(
            DateTimeOffset now,
            TimeSpan lease,
            int batchSize,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutboxEnvelope>>(messages);

        public Task<bool> RenewLeaseAsync(
            OutboxEnvelope message,
            DateTimeOffset now,
            TimeSpan lease,
            CancellationToken cancellationToken) => Task.FromResult(OwnsLease);

        public Task MarkPublishedAsync(
            OutboxEnvelope message,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            Published.Add(message);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            OutboxEnvelope message,
            DateTimeOffset nextAttemptAt,
            string error,
            CancellationToken cancellationToken)
        {
            Failed.Add(new FailedMessage(message, nextAttemptAt, error));
            return Task.CompletedTask;
        }
    }

    private sealed class FakePublisher : IOrderConfirmedPublisher
    {
        public Exception? Failure { get; init; }
        public List<OutboxEnvelope> Messages { get; } = [];

        public Task PublishAsync(OutboxEnvelope message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record FailedMessage(OutboxEnvelope Message, DateTimeOffset NextAttemptAt, string Error);
}
