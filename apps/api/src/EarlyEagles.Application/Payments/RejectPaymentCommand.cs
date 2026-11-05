using Mediator;

namespace EarlyEagles.Application.Payments;

/// <summary>
/// An admin can't find the money (P1-009.2). The member sees the reason and can
/// report again. No such payment: 404. Already confirmed: 409.
/// </summary>
public sealed record RejectPaymentCommand(Guid SessionId, string MemberClerkUserId, string ReviewedBy, string Reason) : ICommand;
