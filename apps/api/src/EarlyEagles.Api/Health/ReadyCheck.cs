namespace EarlyEagles.Api.Health;

/// <summary>One entry in <see cref="ReadyResponse.Checks"/> — the outcome of a
/// single registered <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/>.</summary>
public sealed record ReadyCheck(
    string Name,
    string Status,
    string? Description,
    long DurationMs);
