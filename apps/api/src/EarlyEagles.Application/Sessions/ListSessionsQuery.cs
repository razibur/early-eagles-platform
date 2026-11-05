using Mediator;

namespace EarlyEagles.Application.Sessions;

/// <summary>
/// The ambient tenant's sessions in one ISO week, cancelled ones included so a
/// member can see the Sunday is off (P1-005).
/// </summary>
/// <remarks>
/// The week is required: "this week" depends on the club's time zone, which the
/// platform doesn't hold yet, and the member's device already knows it.
/// </remarks>
public sealed record ListSessionsQuery(string Week) : IQuery<IReadOnlyList<SessionSummary>>;
