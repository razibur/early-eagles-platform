namespace EarlyEagles.Api.Health;

/// <summary>
/// Response body for <c>GET /ready</c>. Reports the overall status and each
/// registered <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/>
/// individually so an oncall human can see which dependency tripped the readiness
/// probe (database, Service Bus, Redis, etc.).
/// </summary>
public sealed record ReadyResponse(
    string Status,
    IReadOnlyList<ReadyCheck> Checks,
    long TotalDurationMs);
