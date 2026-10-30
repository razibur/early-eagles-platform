using EarlyEagles.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace EarlyEagles.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> used when no Resend API key is configured: a
/// laptop, a test host, an environment before P0-011. It logs that an email
/// would have gone out and sends nothing, so callers run end to end without
/// an account.
/// </summary>
/// <remarks>
/// It logs the subject and recipient count only. Addresses are personal data
/// and stay out of the logs.
/// </remarks>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => this.logger = logger;

    public Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        LoggingEmailSenderLog.NotSent(logger, message.Subject, message.To.Count);

        return Task.FromResult<string?>(null);
    }
}
