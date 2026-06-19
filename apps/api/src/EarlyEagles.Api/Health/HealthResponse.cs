namespace EarlyEagles.Api.Health;

/// <summary>
/// Response body for <c>GET /health</c>. Kept as a small, stable contract because
/// uptime monitoring (Better Stack, App Insights probe — P0-092) parses this shape.
/// </summary>
/// <param name="Status">Always <c>"Healthy"</c> when reached; the response is only
/// served once the host is up.</param>
/// <param name="Version">Informational assembly version of the API (Git SHA suffix
/// when set by CI; bare semver in dev).</param>
/// <param name="UptimeSeconds">Seconds since the process started.</param>
public sealed record HealthResponse(string Status, string Version, long UptimeSeconds);
