using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace EarlyEagles.Api.Tenancy;

/// <summary>
/// Parses the request host for a tenant subdomain, resolves the tenant via
/// <see cref="ITenantLookup"/>, and populates <see cref="ITenantContextInitializer"/>
/// for downstream handlers.
/// <para>
/// In <b>Production</b> a resolvable tenant is mandatory for tenant-scoped
/// requests: a missing subdomain short-circuits with <c>400 Bad Request</c> and
/// an unknown slug with <c>404 Not Found</c> (both RFC 7807 ProblemDetails).
/// Outside Production the middleware stays lenient — misses pass through
/// unresolved so <c>*.localhost</c> and IP-based dev hosts keep working
/// (P0-031.4 covers the resolved-tenant E2E path).
/// </para>
/// <para>
/// Infrastructure endpoints in <see cref="TenantAgnosticPathPrefixes"/> are never
/// gated: uptime probes hit <c>/health</c> and <c>/ready</c> on the bare domain,
/// so they must resolve without a tenant even in Production. Webhooks
/// (<c>/api/webhooks</c>: Clerk P0-036, Stripe P0-043) call the bare API host too
/// and carry their tenant in the payload.
/// </para>
/// </summary>
public sealed class TenantResolutionMiddleware : IMiddleware
{
    private static readonly string[] TenantAgnosticPathPrefixes =
    [
        "/health",
        "/ready",

        // API docs are infrastructure, not tenant-scoped: served in non-Production
        // and left for routing to 404 in Production (they are not mapped there).
        // Gating them would mask that 404 behind a 400 tenant error.
        "/openapi",
        "/swagger",

        // Third-party webhooks, authenticated by their own signatures.
        "/api/webhooks",
    ];

    private readonly ITenantLookup lookup;
    private readonly ITenantContextInitializer initializer;
    private readonly IHostEnvironment environment;
    private readonly IProblemDetailsService problemDetailsService;

    public TenantResolutionMiddleware(
        ITenantLookup lookup,
        ITenantContextInitializer initializer,
        IHostEnvironment environment,
        IProblemDetailsService problemDetailsService)
    {
        this.lookup = lookup;
        this.initializer = initializer;
        this.environment = environment;
        this.problemDetailsService = problemDetailsService;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        // Resolution is always best-effort so /health etc. still populate the
        // context when a subdomain is present. Enforcement (the 400 / 404) is
        // what the allow-list and the environment gate suppress.
        var enforce = environment.IsProduction() && !IsTenantAgnostic(context.Request.Path);

        if (!SubdomainExtractor.TryExtract(context.Request.Host.Value, out var slug))
        {
            if (enforce)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Tenant not specified",
                    "Requests must target a tenant subdomain, e.g. '<tenant>.earlyeagles.club'.")
                    .ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
            return;
        }

        var result = await lookup
            .FindBySlugAsync(slug, context.RequestAborted)
            .ConfigureAwait(false);

        if (result is null)
        {
            if (enforce)
            {
                await WriteProblemAsync(
                    context,
                    StatusCodes.Status404NotFound,
                    "Tenant not found",
                    $"No tenant is registered for the subdomain '{slug}'.")
                    .ConfigureAwait(false);
                return;
            }

            await next(context).ConfigureAwait(false);
            return;
        }

        initializer.Initialize(result.TenantId, result.Slug, result.ClerkOrganizationId);
        await next(context).ConfigureAwait(false);
    }

    private static bool IsTenantAgnostic(PathString path)
    {
        foreach (var prefix in TenantAgnosticPathPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task WriteProblemAsync(HttpContext context, int statusCode, string title, string detail)
    {
        context.Response.StatusCode = statusCode;
        await problemDetailsService
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                    Type = "https://datatracker.ietf.org/doc/html/rfc7807#section-3.1",
                },
            })
            .ConfigureAwait(false);
    }
}
