namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// Hands a queued <see cref="OutboxMessage"/> to the message bus (P0-050.2).
/// </summary>
/// <remarks>
/// <para>
/// Lives in Infrastructure rather than beside the Application ports, for the
/// same reason <see cref="OutboxMessage"/> does: the outbox is transport
/// machinery, not a business concept, and nothing in the Application layer
/// consumes this. Its only caller is <see cref="OutboxProcessor"/>.
/// </para>
/// <para>
/// Implementations must be safe to call again for the same message. The drain
/// stamps <see cref="OutboxMessage.ProcessedAt"/> only after a successful
/// publish, so a process that dies between the publish and the commit will
/// re-send on the next tick. That is the deliberate trade in the outbox
/// pattern: at-least-once delivery, never at-most-once, because a lost event is
/// worse than a repeated one.
/// </para>
/// </remarks>
public interface IOutboxPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}
