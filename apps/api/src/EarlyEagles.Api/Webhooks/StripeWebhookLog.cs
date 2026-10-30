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

    [LoggerMessage(
        EventId = 1605,
        Level = LogLevel.Information,
        Message = "Tenant {TenantId} subscription {SubscriptionId} recorded: {Started} ({EventId})")]
    public static partial void SubscriptionStarted(ILogger logger, Guid tenantId, string subscriptionId, bool started, string eventId);

    // Error, not Warning: someone paid and no tenant recorded it. Someone has to look.
    [LoggerMessage(
        EventId = 1606,
        Level = LogLevel.Error,
        Message = "Checkout session {SessionId} not recorded: {Reason} ({EventId})")]
    public static partial void CheckoutSkipped(ILogger logger, string sessionId, string reason, string eventId);

    [LoggerMessage(
        EventId = 1607,
        Level = LogLevel.Information,
        Message = "Tenant {TenantId} invoice {InvoiceId} payment failed; marked past due: {MarkedPastDue} ({EventId})")]
    public static partial void PaymentFailed(ILogger logger, Guid tenantId, string invoiceId, bool markedPastDue, string eventId);

    // Error: a customer's payment failed and no tenant reflects it. Someone has to look.
    [LoggerMessage(
        EventId = 1608,
        Level = LogLevel.Error,
        Message = "Failed invoice {InvoiceId} not recorded: {Reason} ({EventId})")]
    public static partial void PaymentFailureSkipped(ILogger logger, string invoiceId, string reason, string eventId);
}
