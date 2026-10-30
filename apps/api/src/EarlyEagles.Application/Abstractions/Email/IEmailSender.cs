namespace EarlyEagles.Application.Abstractions.Email;

/// <summary>
/// Sends transactional email (P0-053.1). Infrastructure provides Resend
/// (<c>ResendEmailSender</c>) when an API key is configured, and a logging
/// stand-in that sends nothing when it is not.
/// </summary>
/// <remarks>
/// Call it from outbox consumers, not from request handlers: a provider
/// outage then delays the email instead of failing the request, and the
/// message's <see cref="EmailMessage.IdempotencyKey"/> keeps a redelivery from
/// sending it twice.
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="message"/> and returns the provider's id for it,
    /// or <see langword="null"/> when no provider is configured and nothing
    /// was sent.
    /// </summary>
    /// <exception cref="EmailDeliveryException">The provider refused the email or could not be reached.</exception>
    Task<string?> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
