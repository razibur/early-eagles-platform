using System.Text.Json.Serialization;
using EarlyEagles.Domain.Payments;

namespace EarlyEagles.Application.Payments;

/// <summary>
/// One member the admin still has to deal with for a session (P1-009.3).
/// <c>Status</c> is <c>null</c> for someone who said they'd come and hasn't
/// reported a payment; the amount is then the session's fee.
/// </summary>
public sealed record UnsettledPaymentRow(
    string ClerkUserId,
    string? DisplayName,
    int AmountCents,
    [property: JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))] PaymentStatus? Status,
    string? SenderName,
    string? Reference,
    string? RejectionReason);
