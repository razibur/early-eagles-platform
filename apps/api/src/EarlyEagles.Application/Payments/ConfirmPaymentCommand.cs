using Mediator;

namespace EarlyEagles.Application.Payments;

/// <summary>
/// An admin saw the money arrive (P1-009). Confirming again changes nothing and
/// raises no second event. No such payment: 404. Rejected: 409, the member
/// reports again first.
/// </summary>
public sealed record ConfirmPaymentCommand(Guid SessionId, string MemberClerkUserId, string ReviewedBy) : ICommand;
