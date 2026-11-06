using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Domain.Sessions;

/// <summary>
/// One club play session, usually a Sunday morning (P1-001.1). The admin
/// creates each one (no recurring schedule) and sets its fee, which members
/// pay by Zelle and report per session (P1-001.3).
/// </summary>
/// <remarks>
/// <see cref="Date"/> and <see cref="StartTime"/> are the club's local wall
/// clock; the tenant's time zone arrives when something needs the instant
/// (the RSVP cut-off, P1-001.2). The fee is whole cents in the club's currency
/// (US dollars for Early Eagles).
/// </remarks>
public sealed class Session : AggregateRoot<Guid>, ITenantScoped
{
    public const int VenueMaxLength = 200;

    /// <summary>Early Eagles' Sunday fee: $15.</summary>
    public const int DefaultFeeCents = 1500;

    /// <summary>A sanity cap ($1,000) that catches a fee typed in dollars as cents the other way round.</summary>
    public const int MaxFeeCents = 100_000;

    private Session(Guid id, Guid tenantId, DateOnly date, TimeOnly startTime, string venue, int feeCents, DateTimeOffset createdAt)
        : base(id)
    {
        TenantId = tenantId;
        Date = date;
        StartTime = startTime;
        Venue = venue;
        FeeCents = feeCents;
        Status = SessionStatus.Scheduled;
        CreatedAt = createdAt;
    }

    // Parameterless ctor for EF Core materialisation.
    private Session()
    {
        Venue = string.Empty;
    }

    public Guid TenantId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public string Venue { get; private set; }

    public int FeeCents { get; private set; }

    public SessionStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Session Create(
        Guid id,
        Guid tenantId,
        DateOnly date,
        TimeOnly startTime,
        string venue,
        int feeCents,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Session id must not be empty.", nameof(id));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId));
        }

        return new Session(id, tenantId, date, startTime, ValidateVenue(venue), ValidateFee(feeCents), createdAt);
    }

    /// <summary>True when <paramref name="venue"/> is non-blank and fits once trimmed.</summary>
    public static bool IsValidVenue(string? venue) =>
        venue is not null && venue.Trim().Length is > 0 and <= VenueMaxLength;

    /// <summary>True when <paramref name="feeCents"/> is between 0 (free) and <see cref="MaxFeeCents"/>.</summary>
    public static bool IsValidFee(int feeCents) => feeCents is >= 0 and <= MaxFeeCents;

    /// <summary>
    /// Changes when, where or how much. Only a scheduled session can change;
    /// a cancelled one is history. Returns whether anything changed.
    /// </summary>
    public bool UpdateDetails(DateOnly date, TimeOnly startTime, string venue, int feeCents)
    {
        if (Status is SessionStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot change a cancelled session.");
        }

        var newVenue = ValidateVenue(venue);
        var newFee = ValidateFee(feeCents);
        if (Date == date && StartTime == startTime && Venue == newVenue && FeeCents == newFee)
        {
            return false;
        }

        Date = date;
        StartTime = startTime;
        Venue = newVenue;
        FeeCents = newFee;
        return true;
    }

    /// <summary>
    /// True until the session starts, unless it is cancelled. The caller supplies
    /// "now" on the club's wall clock, the same clock as <see cref="Date"/> and
    /// <see cref="StartTime"/>, so the rule needs no time zone of its own.
    /// </summary>
    public bool AcceptsRsvps(DateTime clubLocalNow) =>
        Status is SessionStatus.Scheduled && !HasStarted(clubLocalNow);

    /// <summary>True from the start time on, on the club's wall clock.</summary>
    public bool HasStarted(DateTime clubLocalNow) => clubLocalNow >= Date.ToDateTime(StartTime);

    /// <summary>Cancels the session. Returns <c>false</c> when it was already cancelled.</summary>
    public bool Cancel()
    {
        if (Status is SessionStatus.Cancelled)
        {
            return false;
        }

        Status = SessionStatus.Cancelled;
        return true;
    }

    private static string ValidateVenue(string venue)
    {
        if (!IsValidVenue(venue))
        {
            throw new ArgumentException($"Venue must be non-blank and at most {VenueMaxLength} characters.", nameof(venue));
        }

        return venue.Trim();
    }

    private static int ValidateFee(int feeCents)
    {
        if (!IsValidFee(feeCents))
        {
            throw new ArgumentOutOfRangeException(nameof(feeCents), feeCents, $"Fee must be between 0 and {MaxFeeCents} cents.");
        }

        return feeCents;
    }
}
