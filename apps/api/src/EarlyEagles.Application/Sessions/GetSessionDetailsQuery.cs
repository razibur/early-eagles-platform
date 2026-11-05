using Mediator;

namespace EarlyEagles.Application.Sessions;

/// <summary>One session for one member (P1-005.2). Unknown here or in another club: 404.</summary>
public sealed record GetSessionDetailsQuery(Guid SessionId, string ClerkUserId) : IQuery<SessionDetails>;
