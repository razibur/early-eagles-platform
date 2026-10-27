using Microsoft.Extensions.Logging;

namespace EarlyEagles.Infrastructure.Messaging;

/// <summary>
/// Source-generated logging methods for <see cref="PaymentConfirmedHandler"/>.
/// Own file for StyleCop's one-type-per-file rule (SA1402).
/// </summary>
internal static partial class PaymentConfirmedHandlerLog
{
    [LoggerMessage(EventId = 1300, Level = LogLevel.Information, Message = "Listening on {Topic}/{Subscription}")]
    public static partial void Started(ILogger logger, string topic, string subscription);

    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Information,
        Message = "Received {EventType} ({MessageId}) for tenant {TenantId}, delivery {DeliveryCount}")]
    public static partial void Received(ILogger logger, string eventType, string messageId, string tenantId, int deliveryCount);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Error, Message = "Service Bus receive failed ({ErrorSource}) on {EntityPath}")]
    public static partial void ReceiveFailed(ILogger logger, string errorSource, string entityPath, Exception exception);
}
