namespace EarlyEagles.Application.Abstractions.Payments;

/// <summary>What the confirmation email says (P1-010). <c>MemberName</c> is <c>null</c> when Clerk has no name for them.</summary>
public sealed record PaymentEmailFacts(
    string ClubName,
    IReadOnlyList<string> AdminEmails,
    string? MemberName,
    DateOnly SessionDate,
    string Venue,
    int AmountCents,
    string SenderName);
