using Mediator;

namespace EarlyEagles.Application.Sessions;

/// <summary>
/// Every member of the club against one session, answered or not, so the admin
/// sees who to chase (P1-007.2). Unknown here or in another club: 404.
/// </summary>
public sealed record GetRsvpGridQuery(Guid SessionId) : IQuery<IReadOnlyList<RsvpGridRow>>;
