using Microsoft.Extensions.Logging;

namespace EarlyEagles.Infrastructure.Outbox;

/// <summary>
/// Source-generated logging methods for <see cref="LoggingOutboxPublisher"/>.
/// Lives in its own file so StyleCop's "one type per file" rule (SA1402) stays
/// green.
/// </summary>
internal static partial class LoggingOutboxPublisherLog
{
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Outbox message {EventType} ({MessageId}) for tenant {TenantId} was not delivered: no message bus is configured yet")]
    public static partial void Published(ILogger logger, string eventType, Guid messageId, Guid tenantId);
}
