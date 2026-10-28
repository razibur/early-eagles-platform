using Azure.Monitor.OpenTelemetry.AspNetCore;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EarlyEagles.Api.Observability;

/// <summary>
/// OpenTelemetry traces and metrics (P0-089.1). Where they go is decided by
/// configuration, so the same build runs everywhere:
/// <list type="bullet">
///   <item><c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> set (Azure; environment.bicep
///   passes it): the Azure Monitor distro, which also ships ILogger logs.</item>
///   <item><c>OTEL_EXPORTER_OTLP_ENDPOINT</c> set (local): OTLP, e.g. to the free
///   Aspire dashboard container.</item>
///   <item>neither: telemetry is collected and dropped, at negligible cost.</item>
/// </list>
/// </summary>
public static class TelemetryExtensions
{
    public const string AzureMonitorConnectionStringKey = "APPLICATIONINSIGHTS_CONNECTION_STRING";
    public const string OtlpEndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // Probe endpoints Container Apps hits every few seconds; tracing them
    // would bury real requests and cost ingestion for nothing.
    private static readonly string[] UntracedPaths = ["/health", "/ready"];

    public static IHostApplicationBuilder AddTelemetry(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var useAzureMonitor = !string.IsNullOrWhiteSpace(builder.Configuration[AzureMonitorConnectionStringKey]);
        var useOtlp = !string.IsNullOrWhiteSpace(builder.Configuration[OtlpEndpointKey]);

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ApplicationInfo.ServiceName, serviceVersion: ApplicationInfo.Version))
            .WithTracing(tracing =>
            {
                // Database calls, in every configuration.
                tracing.AddNpgsql();

                // The distro already adds ASP.NET Core and HttpClient
                // instrumentation; adding them again would double-count.
                if (!useAzureMonitor)
                {
                    tracing.AddAspNetCoreInstrumentation(options => options.Filter = IsTraced);
                    tracing.AddHttpClientInstrumentation();
                }
            })
            .WithMetrics(metrics =>
            {
                if (!useAzureMonitor)
                {
                    metrics.AddAspNetCoreInstrumentation();
                    metrics.AddHttpClientInstrumentation();
                }
            });

        // Log scopes (TenantId, TenantSlug, UserId from TelemetryEnrichmentMiddleware,
        // P0-090) become attributes on exported logs, i.e. queryable fields.
        builder.Services.Configure<OpenTelemetryLoggerOptions>(options => options.IncludeScopes = true);

        if (useAzureMonitor)
        {
            telemetry.UseAzureMonitor();

            // Apply the probe filter to the distro's own ASP.NET Core instrumentation.
            builder.Services.Configure<OpenTelemetry.Instrumentation.AspNetCore.AspNetCoreTraceInstrumentationOptions>(
                options => options.Filter = IsTraced);
        }

        if (useOtlp)
        {
            // The distro registers the log pipeline itself; without it, add one
            // so OTLP carries logs as well as traces and metrics.
            if (!useAzureMonitor)
            {
                telemetry.WithLogging();
            }

            telemetry.UseOtlpExporter();
        }

        // Structured stdout everywhere but a developer's console: one JSON object
        // per line, scopes included. Container Apps forwards stdout to Log
        // Analytics (ContainerAppConsoleLogs), where each field is queryable.
        if (!builder.Environment.IsDevelopment())
        {
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            });
        }

        return builder;
    }

    private static bool IsTraced(HttpContext context) =>
        !UntracedPaths.Any(path => context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase));
}
