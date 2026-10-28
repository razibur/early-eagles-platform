using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EarlyEagles.Api.Health;

/// <summary>
/// Maps the API's diagnostic endpoints:
/// <list type="bullet">
///   <item><c>/health</c> — liveness. 200 as long as the host is up. Custom
///   payload with assembly version + process uptime. No auth.</item>
///   <item><c>/ready</c> — readiness. Runs every registered
///   <see cref="IHealthCheck"/> and reports the aggregate + per-check details.
///   503 if any check is Unhealthy. Today: the database
///   (<c>PostgresHealthCheck</c>, P0-026.2); Service Bus joins with the real
///   publisher (P0-050.2.2). No auth, so descriptions are fixed text.</item>
/// </list>
/// </summary>
public static class HealthEndpoints
{
    // Captured when the type is first touched, which happens during host
    // composition — close enough to "process start" for the uptime number
    // to be meaningful without reaching into Process.GetCurrentProcess.
    private static readonly DateTimeOffset ApplicationStarted = DateTimeOffset.UtcNow;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapGet(
                "/health",
                () => new HealthResponse(
                    Status: "Healthy",
                    Version: ApplicationInfo.Version,
                    UptimeSeconds: (long)(DateTimeOffset.UtcNow - ApplicationStarted).TotalSeconds))
            .WithName("Health")
            .WithTags("Diagnostics");

        routes
            .MapHealthChecks(
                "/ready",
                new HealthCheckOptions { ResponseWriter = WriteReadyResponseAsync })
            .WithName("Ready")
            .WithTags("Diagnostics");

        return routes;
    }

    private static Task WriteReadyResponseAsync(HttpContext context, HealthReport report)
    {
        var response = new ReadyResponse(
            Status: report.Status.ToString(),
            Checks: report.Entries
                .Select(e => new ReadyCheck(
                    Name: e.Key,
                    Status: e.Value.Status.ToString(),
                    Description: e.Value.Description,
                    DurationMs: (long)e.Value.Duration.TotalMilliseconds))
                .ToArray(),
            TotalDurationMs: (long)report.TotalDuration.TotalMilliseconds);

        context.Response.ContentType = MediaTypeNames.Application.Json;
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions),
            context.RequestAborted);
    }
}
