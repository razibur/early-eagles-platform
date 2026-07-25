using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Tenancy;

namespace EarlyEagles.Api.Tenancy;

/// <summary>
/// Parses the request host for a tenant subdomain, resolves the tenant via
/// <see cref="ITenantLookup"/>, and populates <see cref="ITenantContextInitializer"/>
/// for downstream handlers. Misses (no subdomain, unknown slug) are tolerated
/// here — the 404 / 400 status code handling is layered on in P0-031.3.
/// </summary>
public sealed class TenantResolutionMiddleware : IMiddleware
{
    private readonly ITenantLookup lookup;
    private readonly ITenantContextInitializer initializer;

    public TenantResolutionMiddleware(
        ITenantLookup lookup,
        ITenantContextInitializer initializer)
    {
        this.lookup = lookup;
        this.initializer = initializer;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (SubdomainExtractor.TryExtract(context.Request.Host.Value, out var slug))
        {
            var result = await lookup
                .FindBySlugAsync(slug, context.RequestAborted)
                .ConfigureAwait(false);

            if (result is not null)
            {
                initializer.Initialize(result.TenantId, result.Slug);
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
