namespace EarlyEagles.Application.Abstractions.Email;

/// <summary>
/// One transactional email (P0-053.1). The sender address is not here: it is
/// the platform's configured <c>From</c>, so a handler cannot send as anyone.
/// </summary>
/// <param name="To">Recipient addresses, <c>a@b.c</c> or <c>Name &lt;a@b.c&gt;</c>. At least one.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="Html">HTML body.</param>
/// <param name="Text">Plain-text body; providers derive one from the HTML when absent.</param>
/// <param name="ReplyTo">Where replies go instead of the sender address, e.g. a club admin.</param>
/// <param name="IdempotencyKey">
/// Same key, same email: a retry with it is not sent twice. Use something
/// stable per logical email, such as <c>"payment-confirmed/{outboxMessageId}"</c>.
/// Resend keeps keys for 24 hours and caps them at 256 characters.
/// </param>
public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string Html,
    string? Text = null,
    string? ReplyTo = null,
    string? IdempotencyKey = null);
