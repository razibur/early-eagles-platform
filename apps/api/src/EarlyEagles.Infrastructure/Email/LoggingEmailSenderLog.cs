using Microsoft.Extensions.Logging;

namespace EarlyEagles.Infrastructure.Email;

/// <summary>Source-generated logging for <see cref="LoggingEmailSender"/>.</summary>
internal static partial class LoggingEmailSenderLog
{
    [LoggerMessage(
        EventId = 1501,
        Level = LogLevel.Information,
        Message = "Email \"{Subject}\" for {RecipientCount} recipient(s) was not sent: Resend:ApiKey is not set")]
    public static partial void NotSent(ILogger logger, string subject, int recipientCount);
}
