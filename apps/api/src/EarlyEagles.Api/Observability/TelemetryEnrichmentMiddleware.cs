using System.Diagnostics;
using System.Security.Claims;
using EarlyEagles.Application.Abstractions.Tenancy;

namespace EarlyEagles.Api.Observability;

/// <summary>
/// Marks everything a request produces with who it was for, so traces and
/// logs can be filtered to one club or one member:
/// <list type="bullet">
///   <item>The request's trace span gets <c>tenant.id</c>, <c>tenant.slug</c> and
///   <c>user.id</c> tags (P0-089.2).</item>
///   <item>Every log line written while the request runs is inside a scope with
///   <c>TenantId</c>, <c>TenantSlug</c> and <c>UserId</c> (P0-090). The JSON console
///   formatter and the OpenTelemetry log exporter both include scopes, so in Log
///   Analytics / App Insights they are ordinary queryable fields.</item>
/// </list>
/// Runs right after <c>TenantResolutionMiddleware</c>. <c>user.id</c> is the
/// principal's <c>sub</c> claim (the Clerk user id once auth lands, P0-035).
/// Identifiers only, never names, emails or other personal data.
/// </summary>
public sealed class TelemetryEnrichmentMiddleware : IMiddleware
{
    public const string TenantIdTag = "tenant.id";
    public const string TenantSlugTag = "tenant.slug";
    public const string UserIdTag = "user.id";

    private readonly ITenantContext tenant;
    private readonly ILogger<TelemetryEnrichmentMiddleware> logger;

    public TelemetryEnrichmentMiddleware(ITenantContext tenant, ILogger<TelemetryEnrichmentMiddleware> logger)
    {
        this.tenant = tenant;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var userId = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        // The ASP.NET Core instrumentation's span for this request. Null when
        // nothing is listening (no exporter), which costs nothing.
        var activity = Activity.Current;
        if (activity is not null)
        {
            if (tenant.IsResolved)
            {
                activity.SetTag(TenantIdTag, tenant.TenantId.ToString());
                activity.SetTag(TenantSlugTag, tenant.Slug);
            }

            if (!string.IsNullOrEmpty(userId))
            {
                activity.SetTag(UserIdTag, userId);
            }
        }

        var scope = new Dictionary<string, object?>(capacity: 3);
        if (tenant.IsResolved)
        {
            scope["TenantId"] = tenant.TenantId;
            scope["TenantSlug"] = tenant.Slug;
        }

        if (!string.IsNullOrEmpty(userId))
        {
            scope["UserId"] = userId;
        }

        if (scope.Count == 0)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        using (logger.BeginScope(scope))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
