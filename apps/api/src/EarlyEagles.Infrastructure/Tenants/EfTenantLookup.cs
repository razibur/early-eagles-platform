using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Tenants;

/// <summary>
/// EF Core <see cref="ITenantLookup"/> (P0-031.5): the tenant-resolution
/// middleware's slug → tenant read against the <c>tenants</c> table.
/// </summary>
/// <remarks>
/// <para>A deactivated tenant does not resolve: its subdomain answers like an
/// unknown one. Past-due and suspended tenants still resolve, so they can be
/// told why they're limited (dunning, P2-020).</para>
/// <para>Like <see cref="EfClerkTenantDirectory"/>, it runs before any tenant is
/// resolved, so it bypasses the query filter and opens its own short-lived
/// context: the request's context snapshots the tenant when it's created and
/// must not be created before resolution.</para>
/// <para>One indexed read per tenant-scoped request (unique index on
/// <c>slug</c>). No cache yet; add one when traffic says so.</para>
/// </remarks>
public sealed class EfTenantLookup : ITenantLookup
{
    private readonly DbContextOptions<ApplicationDbContext> options;

    public EfTenantLookup(DbContextOptions<ApplicationDbContext> options)
    {
        this.options = options;
    }

    public async Task<TenantLookupResult?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);

        // Slugs are stored lowercase; the extractor already lowercases the host.
        var normalized = slug.ToLowerInvariant();

        await using var context = new ApplicationDbContext(options, new TenantContext());
        return await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.Slug == normalized && t.Status != TenantStatus.Deactivated)
            .Select(t => new TenantLookupResult(t.Id, t.Slug))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
