using System.Reflection;

namespace EarlyEagles.Api.Health;

/// <summary>
/// Maps diagnostic endpoints (<c>/health</c> for liveness, <c>/ready</c> for
/// readiness in P0-026.2). These are deliberately *not* under <c>/api</c> and
/// are anonymous — uptime monitoring needs them reachable without auth.
/// </summary>
public static class HealthEndpoints
{
    // Captured when the type is first touched, which happens during host
    // composition — close enough to "process start" for the uptime number
    // to be meaningful without reaching into Process.GetCurrentProcess.
    private static readonly DateTimeOffset ApplicationStarted = DateTimeOffset.UtcNow;

    private static readonly string ApplicationVersion = ResolveVersion();

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes
            .MapGet(
                "/health",
                () => new HealthResponse(
                    Status: "Healthy",
                    Version: ApplicationVersion,
                    UptimeSeconds: (long)(DateTimeOffset.UtcNow - ApplicationStarted).TotalSeconds))
            .WithName("Health")
            .WithTags("Diagnostics");

        return routes;
    }

    private static string ResolveVersion()
    {
        var assembly = typeof(HealthEndpoints).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}
