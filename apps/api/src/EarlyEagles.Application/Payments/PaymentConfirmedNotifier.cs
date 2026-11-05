using System.Globalization;
using System.Net;
using EarlyEagles.Application.Abstractions.Email;
using EarlyEagles.Application.Abstractions.Payments;
using EarlyEagles.Domain.Payments;

namespace EarlyEagles.Application.Payments;

/// <summary>
/// Tells a club's admins that a payment was confirmed (P1-010). English only
/// (ADR-0003). Sends through <see cref="IEmailSender"/>, which logs instead of
/// sending until Resend is set up (P0-011).
/// </summary>
/// <remarks>
/// The idempotency key comes from the bus message, so a redelivery isn't sent
/// twice. A delivery failure propagates, so the bus retries the message.
/// </remarks>
public sealed class PaymentConfirmedNotifier
{
    private readonly IPaymentEmailDirectory directory;
    private readonly IEmailSender emailSender;

    public PaymentConfirmedNotifier(IPaymentEmailDirectory directory, IEmailSender emailSender)
    {
        this.directory = directory;
        this.emailSender = emailSender;
    }

    public async Task NotifyAsync(PaymentConfirmed confirmed, string messageId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(confirmed);

        var facts = await directory.FindAsync(confirmed.TenantId, confirmed.PaymentId, cancellationToken).ConfigureAwait(false);
        if (facts is null || facts.AdminEmails.Count == 0)
        {
            return;
        }

        var member = facts.MemberName ?? "A member";
        var date = facts.SessionDate.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture);
        var amount = string.Create(CultureInfo.InvariantCulture, $"${facts.AmountCents / 100m:0.00}");
        var text = $"{member} paid {amount} for {date} at {facts.Venue}. The Zelle transfer was from {facts.SenderName}.";

        await emailSender
            .SendAsync(
                new EmailMessage(
                    facts.AdminEmails,
                    $"{facts.ClubName}: payment confirmed for {member}",
                    $"<p>{WebUtility.HtmlEncode(text)}</p>",
                    text,
                    IdempotencyKey: $"payment-confirmed/{messageId}"),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
