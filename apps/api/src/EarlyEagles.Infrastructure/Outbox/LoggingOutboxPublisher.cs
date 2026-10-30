using Microsoft.Extensions.Logging;

namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// Placeholder <see cref="IOutboxPublisher"/> that logs each message instead of
/// sending it, standing in until the Service Bus topic exists (P0-051, which
/// waits on the Bicep work in P0-058.4): a real registration that keeps the
/// pipeline whole and honest about what it does not do yet.
/// </summary>
/// <remarks>
/// It reports success, so the drain stamps rows processed and the queue moves.
/// That is the point — the publish/mark-processed path is exercised end to end
/// on a laptop with no Azure account — but it means **events are not delivered
/// anywhere** while this implementation is registered. Swapping in the Service
/// Bus publisher is a registration change, nothing more.
/// </remarks>
public sealed class LoggingOutboxPublisher : IOutboxPublisher
{
    private readonly ILogger<LoggingOutboxPublisher> logger;

    public LoggingOutboxPublisher(ILogger<LoggingOutboxPublisher> logger)
    {
        this.logger = logger;
    }

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        LoggingOutboxPublisherLog.Published(logger, message.EventType, message.Id, message.TenantId);

        return Task.CompletedTask;
    }
}
