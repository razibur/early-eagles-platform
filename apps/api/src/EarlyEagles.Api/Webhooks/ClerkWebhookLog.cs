using EarlyEagles.Infrastructure.Identity;

namespace EarlyEagles.Api.Webhooks;

/// <summary>
/// Source-generated logging methods for <see cref="ClerkWebhookEndpoints"/>.
/// Separate file so StyleCop's one-type-per-file rule (SA1402) stays green.
/// </summary>
internal static partial class ClerkWebhookLog
{
    [LoggerMessage(EventId = 1400, Level = LogLevel.Information, Message = "Clerk webhook {EventType} received ({SvixId})")]
    public static partial void Received(ILogger logger, string eventType, string svixId);

    [LoggerMessage(EventId = 1401, Level = LogLevel.Warning, Message = "Clerk webhook rejected: {Reason} ({SvixId})")]
    public static partial void Rejected(ILogger logger, SvixVerificationResult reason, string svixId);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Warning, Message = "Clerk webhook has a valid signature but no event type ({SvixId})")]
    public static partial void Malformed(ILogger logger, string svixId);

    [LoggerMessage(EventId = 1403, Level = LogLevel.Error, Message = "Clerk webhook received but Clerk:WebhookSigningSecret is not set")]
    public static partial void NotConfigured(ILogger logger);
}
