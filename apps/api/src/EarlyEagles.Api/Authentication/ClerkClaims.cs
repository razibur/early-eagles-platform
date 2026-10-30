namespace EarlyEagles.Api.Authentication;

/// <summary>
/// Claim names in a Clerk session token (v2), as they appear on the principal
/// (inbound claim mapping is off).
/// </summary>
public static class ClerkClaims
{
    /// <summary>The Clerk user id, <c>user_…</c>.</summary>
    public const string Subject = "sub";

    /// <summary>The Clerk session id, <c>sess_…</c>.</summary>
    public const string SessionId = "sid";

    /// <summary>The origin that requested the token.</summary>
    public const string AuthorizedParty = "azp";

    /// <summary>The session status; <c>pending</c> until the user finishes sign-in tasks.</summary>
    public const string SessionStatus = "sts";
}
