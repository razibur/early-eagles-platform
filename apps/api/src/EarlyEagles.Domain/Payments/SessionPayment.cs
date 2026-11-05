using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Domain.Payments;

/// <summary>
/// A member's report that they paid for a session by Zelle, and the admin's
/// verdict (P1-001.3). The money never touches the platform: this tracks it, it
/// doesn't collect it. One per member per session; after a rejection the member
/// reports again on the same row.
/// </summary>
/// <remarks>
/// <see cref="AmountCents"/> is the session's fee when reported, so a later edit
/// to the fee doesn't rewrite what this member was asked to pay.
/// </remarks>
public sealed class SessionPayment : AggregateRoot<Guid>, ITenantScoped
{
    public const int SenderNameMaxLength = 100;
    public const int ReferenceMaxLength = 100;
    public const int RejectionReasonMaxLength = 500;

    private SessionPayment(
        Guid id,
        Guid tenantId,
        Guid sessionId,
        string clerkUserId,
        int amountCents,
        string senderName,
        string reference,
        DateTimeOffset reportedAt)
        : base(id)
    {
        TenantId = tenantId;
        SessionId = sessionId;
        ClerkUserId = clerkUserId;
        AmountCents = amountCents;
        SenderName = senderName;
        Reference = reference;
        Status = PaymentStatus.Reported;
        ReportedAt = reportedAt;
    }

    // Parameterless ctor for EF Core materialisation.
    private SessionPayment()
    {
        ClerkUserId = string.Empty;
        SenderName = string.Empty;
        Reference = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public Guid SessionId { get; private set; }

    public string ClerkUserId { get; private set; }

    public int AmountCents { get; private set; }

    /// <summary>The name on the Zelle transfer, which may not be the member's.</summary>
    public string SenderName { get; private set; }

    /// <summary>The Zelle confirmation code or memo.</summary>
    public string Reference { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset ReportedAt { get; private set; }

    /// <summary>The admin who confirmed or rejected it; <c>null</c> while reported.</summary>
    public string? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    /// <summary>Why it was rejected, shown to the member; <c>null</c> unless rejected.</summary>
    public string? RejectionReason { get; private set; }

    public static SessionPayment Report(
        Guid id,
        Guid tenantId,
        Guid sessionId,
        string clerkUserId,
        int amountCents,
        string senderName,
        string reference,
        DateTimeOffset reportedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Payment id must not be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id must not be empty.", nameof(sessionId));
        }

        if (!TenantMember.IsValidClerkUserId(clerkUserId))
        {
            throw new ArgumentException("Clerk user id is not valid.", nameof(clerkUserId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(amountCents);

        var payment = new SessionPayment(
            id,
            tenantId,
            sessionId,
            clerkUserId,
            amountCents,
            ValidateText(senderName, SenderNameMaxLength, nameof(senderName)),
            ValidateText(reference, ReferenceMaxLength, nameof(reference)),
            reportedAt);
        payment.RaiseDomainEvent(new PaymentReported(tenantId, sessionId, id, clerkUserId, amountCents));
        return payment;
    }

    /// <summary>True when <paramref name="senderName"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidSenderName(string? senderName) => IsValidText(senderName, SenderNameMaxLength);

    /// <summary>True when <paramref name="reference"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidReference(string? reference) => IsValidText(reference, ReferenceMaxLength);

    /// <summary>True when <paramref name="reason"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidRejectionReason(string? reason) => IsValidText(reason, RejectionReasonMaxLength);

    /// <summary>Reports again after a rejection, at the session's current fee. Only a rejected payment can.</summary>
    public void ReportAgain(int amountCents, string senderName, string reference, DateTimeOffset reportedAt)
    {
        if (Status is not PaymentStatus.Rejected)
        {
            throw new InvalidOperationException("Only a rejected payment can be reported again.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(amountCents);

        SenderName = ValidateText(senderName, SenderNameMaxLength, nameof(senderName));
        Reference = ValidateText(reference, ReferenceMaxLength, nameof(reference));
        AmountCents = amountCents;
        ReportedAt = reportedAt;
        Status = PaymentStatus.Reported;
        ReviewedBy = null;
        ReviewedAt = null;
        RejectionReason = null;
        RaiseDomainEvent(new PaymentReported(TenantId, SessionId, Id, ClerkUserId, amountCents));
    }

    /// <summary>
    /// Confirms a reported payment. Returns <c>false</c> when it was already
    /// confirmed, so a double click raises no second event. A rejected payment
    /// must be reported again first.
    /// </summary>
    public bool Confirm(string reviewedBy, DateTimeOffset reviewedAt)
    {
        if (Status is PaymentStatus.Confirmed)
        {
            return false;
        }

        if (Status is PaymentStatus.Rejected)
        {
            throw new InvalidOperationException("A rejected payment can't be confirmed.");
        }

        Review(PaymentStatus.Confirmed, reviewedBy, reviewedAt);
        RaiseDomainEvent(new PaymentConfirmed(TenantId, SessionId, Id, ClerkUserId, AmountCents));
        return true;
    }

    /// <summary>Rejects a reported payment. Returns <c>false</c> when it was already rejected. A confirmed one stays confirmed.</summary>
    public bool Reject(string reviewedBy, string reason, DateTimeOffset reviewedAt)
    {
        if (Status is PaymentStatus.Rejected)
        {
            return false;
        }

        if (Status is PaymentStatus.Confirmed)
        {
            throw new InvalidOperationException("A confirmed payment can't be rejected.");
        }

        var trimmed = ValidateText(reason, RejectionReasonMaxLength, nameof(reason));
        Review(PaymentStatus.Rejected, reviewedBy, reviewedAt);
        RejectionReason = trimmed;
        return true;
    }

    private static bool IsValidText(string? value, int maxLength) =>
        value is not null && value.Trim().Length > 0 && value.Trim().Length <= maxLength;

    private static string ValidateText(string value, int maxLength, string name)
    {
        if (!IsValidText(value, maxLength))
        {
            throw new ArgumentException($"Must be non-blank and at most {maxLength} characters.", name);
        }

        return value.Trim();
    }

    private void Review(PaymentStatus status, string reviewedBy, DateTimeOffset reviewedAt)
    {
        if (!TenantMember.IsValidClerkUserId(reviewedBy))
        {
            throw new ArgumentException("Reviewer is not valid.", nameof(reviewedBy));
        }

        Status = status;
        ReviewedBy = reviewedBy;
        ReviewedAt = reviewedAt;
    }
}
