using System.Diagnostics;
using System.Security.Claims;
using EarlyEagles.Application.Abstractions.Tenancy;

namespace EarlyEagles.Api.Observability;

/// <summary>
/// Tags the request's trace span with who it was for (P0-089.2), so traces in
/// App Insights / Jaeger can be filtered to one club or one member:
/// <list type="bullet">
///   <item><c>tenant.id</c>, <c>tenant.slug</c>: once tenant resolution has run.</item>
///   <item><c>user.id</c>: the authenticated principal's <c>sub</c> claim (the Clerk
///   user id once auth lands, P0-035); absent for anonymous requests.</item>
/// </list>
/// Runs right after <c>TenantResolutionMiddleware</c>. Identifiers only, never
/// names, emails or other personal data.
/// </summary>
public sealed class TelemetryEnrichmentMiddleware : IMiddleware
{
    public const string TenantIdTag = "tenant.id";
    public const string TenantSlugTag = "tenant.slug";
    public const string UserIdTag = "user.id";

    private readonly ITenantContext tenant;

    public TelemetryEnrichmentMiddleware(ITenantContext tenant)
    {
        this.tenant = tenant;
    }

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

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

            var userId = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                activity.SetTag(UserIdTag, userId);
            }
        }

        return next(context);
    }
}
