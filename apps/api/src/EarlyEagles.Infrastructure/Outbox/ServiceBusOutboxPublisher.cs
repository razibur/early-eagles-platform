using Azure.Messaging.ServiceBus;

namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// Publishes outbox rows to the <c>domain-events</c> topic (P0-050.2.2),
/// following the message contract set in P0-051:
/// <list type="bullet">
///   <item><c>MessageId</c> = the outbox row id, so the topic's duplicate
///   detection drops the re-send the at-least-once drain can produce.</item>
///   <item><c>Subject</c> = the short event name (<c>PaymentConfirmed</c>);
///   subscriptions route on it with correlation filters.</item>
///   <item><c>EventType</c> (full CLR name) and <c>TenantId</c> as application
///   properties; the JSON payload as the body.</item>
/// </list>
/// </summary>
/// <remarks>
/// A failed send throws; <see cref="OutboxProcessor"/> records the failure on
/// the row and retries on the next tick. The sender is a process-wide
/// singleton: it is thread-safe and holds the AMQP link.
/// </remarks>
public sealed class ServiceBusOutboxPublisher : IOutboxPublisher
{
    public const string EventTypeProperty = "EventType";
    public const string TenantIdProperty = "TenantId";

    private readonly ServiceBusSender sender;

    public ServiceBusOutboxPublisher(ServiceBusSender sender)
    {
        this.sender = sender;
    }

    /// <summary>Maps an outbox row onto the wire format. Public for tests.</summary>
    public static ServiceBusMessage CreateMessage(OutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var busMessage = new ServiceBusMessage(BinaryData.FromString(message.Payload))
        {
            MessageId = message.Id.ToString(),
            Subject = ShortName(message.EventType),
            ContentType = "application/json",
        };
        busMessage.ApplicationProperties[EventTypeProperty] = message.EventType;
        busMessage.ApplicationProperties[TenantIdProperty] = message.TenantId.ToString();
        return busMessage;
    }

    /// <summary>
    /// <c>EarlyEagles.Domain.Payments.PaymentConfirmed</c> → <c>PaymentConfirmed</c>;
    /// a nested type's <c>Outer+Inner</c> → <c>Inner</c>.
    /// </summary>
    public static string ShortName(string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        var start = eventType.LastIndexOfAny(['.', '+']) + 1;
        return eventType[start..];
    }

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return sender.SendMessageAsync(CreateMessage(message), cancellationToken);
    }
}
