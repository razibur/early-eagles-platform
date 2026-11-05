using EarlyEagles.Domain.Primitives;
using EarlyEagles.Domain.Tenants;

namespace EarlyEagles.Domain.Sessions;

/// <summary>
/// One member's answer for one session (P1-001.2). A member has at most one per
/// session, enforced by a unique index; changing their mind updates the row.
/// </summary>
/// <remarks>
/// Whether the session still takes answers is <see cref="Session.AcceptsRsvps"/>,
/// checked by the caller, because it needs the session.
/// </remarks>
public sealed class Rsvp : AggregateRoot<Guid>, ITenantScoped
{
    private Rsvp(Guid id, Guid tenantId, Guid sessionId, string clerkUserId, RsvpResponse response, DateTimeOffset updatedAt)
        : base(id)
    {
        TenantId = tenantId;
        SessionId = sessionId;
        ClerkUserId = clerkUserId;
        Response = response;
        UpdatedAt = updatedAt;
    }

    // Parameterless ctor for EF Core materialisation.
    private Rsvp()
    {
        ClerkUserId = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public Guid SessionId { get; private set; }

    public string ClerkUserId { get; private set; }

    public RsvpResponse Response { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Rsvp Create(Guid id, Guid tenantId, Guid sessionId, string clerkUserId, RsvpResponse response, DateTimeOffset updatedAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Rsvp id must not be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session id must not be empty.", nameof(sessionId));
        }

        if (!TenantMember.IsValidClerkUserId(clerkUserId))
        {
            throw new ArgumentException("Clerk user id is not valid.", nameof(clerkUserId));
        }

        return new Rsvp(id, tenantId, sessionId, clerkUserId, ValidateResponse(response), updatedAt);
    }

    /// <summary>Records a new answer. Returns <c>false</c> when it is the answer already held.</summary>
    public bool Change(RsvpResponse response, DateTimeOffset updatedAt)
    {
        if (ValidateResponse(response) == Response)
        {
            return false;
        }

        Response = response;
        UpdatedAt = updatedAt;
        return true;
    }

    private static RsvpResponse ValidateResponse(RsvpResponse response)
    {
        if (!Enum.IsDefined(response))
        {
            throw new ArgumentException($"Unknown RSVP response: {response}.", nameof(response));
        }

        return response;
    }
}
