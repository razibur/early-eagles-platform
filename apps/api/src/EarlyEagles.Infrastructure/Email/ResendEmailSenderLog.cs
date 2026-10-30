using Microsoft.Extensions.Logging;

namespace EarlyEagles.Infrastructure.Email;

/// <summary>Source-generated logging for <see cref="ResendEmailSender"/>.</summary>
internal static partial class ResendEmailSenderLog
{
    [LoggerMessage(EventId = 1500, Level = LogLevel.Information, Message = "Email {EmailId} accepted by Resend for {RecipientCount} recipient(s)")]
    public static partial void Sent(ILogger logger, string? emailId, int recipientCount);
}
