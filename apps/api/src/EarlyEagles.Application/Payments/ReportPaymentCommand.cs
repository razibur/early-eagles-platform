using Mediator;

namespace EarlyEagles.Application.Payments;

/// <summary>
/// A member says they paid for a session by Zelle (P1-008). Reports again when
/// their last report was rejected. Unknown session: 404. Already reported or
/// confirmed, a cancelled session, or a free one: 409.
/// </summary>
public sealed record ReportPaymentCommand(
    Guid SessionId,
    string ClerkUserId,
    string SenderName,
    string Reference) : ICommand<ReportPaymentResult>;
