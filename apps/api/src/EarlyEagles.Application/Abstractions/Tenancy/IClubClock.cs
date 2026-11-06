namespace EarlyEagles.Application.Abstractions.Tenancy;

/// <summary>
/// The ambient tenant's wall clock, the clock session dates and start times are
/// on (P1-007.1). A read port, so query handlers can use it.
/// </summary>
public interface IClubClock
{
    Task<DateTime> GetNowAsync(CancellationToken cancellationToken);
}
