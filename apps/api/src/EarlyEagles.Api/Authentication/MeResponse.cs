namespace EarlyEagles.Api.Authentication;

/// <summary>Response body for <c>GET /me</c>.</summary>
/// <param name="UserId">The Clerk user id (<c>sub</c>).</param>
/// <param name="SessionId">The Clerk session id (<c>sid</c>), when the token has one.</param>
/// <param name="Organization">The session's active organization; null when none is active.</param>
public sealed record MeResponse(string UserId, string? SessionId, ClerkActiveOrganization? Organization);
