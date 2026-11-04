namespace EarlyEagles.Domain.Sessions;

/// <summary>Where a <see cref="Session"/> stands. Stored by name.</summary>
public enum SessionStatus
{
    Scheduled = 0,
    Cancelled = 1,
}
