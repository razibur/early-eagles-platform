using Mediator;

namespace EarlyEagles.Application.Payments;

/// <summary>
/// Who still needs the admin for a session (P1-009.3): reported payments to
/// review first, then rejected ones, then members who said yes and haven't
/// reported. Confirmed payments are settled and left out. Unknown here or in
/// another club: 404.
/// </summary>
public sealed record GetUnsettledPaymentsQuery(Guid SessionId) : IQuery<IReadOnlyList<UnsettledPaymentRow>>;
