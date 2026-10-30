using EarlyEagles.Infrastructure.Billing;

namespace EarlyEagles.Api.Webhooks;

/// <summary>
/// Source-generated logging methods for <see cref="StripeWebhookEndpoints"/>.
/// Separate file so StyleCop's one-type-per-file rule (SA1402) stays green.
/// </summary>
internal static partial class StripeWebhookLog
{
    [LoggerMessage(EventId = 1600, Level = LogLevel.Information, Message = "Stripe webhook {EventType} received ({EventId}, live mode {Livemode})")]
    public static partial void Received(ILogger logger, string eventType, string eventId, bool livemode);

    [LoggerMessage(EventId = 1601, Level = LogLevel.Warning, Message = "Stripe webhook rejected: {Reason}")]
    public static partial void Rejected(ILogger logger, StripeWebhookVerificationResult reason);

    [LoggerMessage(EventId = 1602, Level = LogLevel.Warning, Message = "Stripe webhook has a valid signature but is not an event")]
    public static partial void Malformed(ILogger logger);

    // Error: every delivery fails until someone changes the endpoint's API
    // version in the Stripe dashboard.
    [LoggerMessage(
        EventId = 1603,
        Level = LogLevel.Error,
        Message = "Stripe webhook signed correctly but sent in another API release train; set the endpoint's API version to {ExpectedApiVersion}")]
    public static partial void ApiVersionMismatch(ILogger logger, string expectedApiVersion);

    [LoggerMessage(EventId = 1604, Level = LogLevel.Error, Message = "Stripe webhook received but Stripe:WebhookSigningSecret is not set")]
    public static partial void NotConfigured(ILogger logger);
}
