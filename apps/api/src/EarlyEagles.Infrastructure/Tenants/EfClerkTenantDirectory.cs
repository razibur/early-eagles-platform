using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Tenants;

/// <summary>
/// EF Core <see cref="IClerkTenantDirectory"/>. Platform work: it runs before
/// any tenant is resolved, so it bypasses the query filter, and it projects ids
/// and slugs only.
/// </summary>
/// <remarks>
/// Opens its own short-lived context instead of using the request's. The
/// request's <see cref="ApplicationDbContext"/> snapshots the tenant when it's
/// constructed; resolving it here, before the webhook sets the tenant, would
/// pin it to "no tenant" for the rest of the request, and the command's reads
/// would then miss the tenant's own row.
/// </remarks>
public sealed class EfClerkTenantDirectory : IClerkTenantDirectory
{
    private readonly DbContextOptions<ApplicationDbContext> options;

    public EfClerkTenantDirectory(DbContextOptions<ApplicationDbContext> options)
    {
        this.options = options;
    }

    public async Task<ClerkTenantDirectoryEntry> FindAsync(string clerkOrganizationId, string slug, CancellationToken cancellationToken)
    {
        await using var context = new ApplicationDbContext(options, new TenantContext());
        var matches = await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.ClerkOrganizationId == clerkOrganizationId || t.Slug == slug)
            .Select(t => new { t.Id, t.Slug, t.ClerkOrganizationId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var linked = matches.SingleOrDefault(t => t.ClerkOrganizationId == clerkOrganizationId);
        var slugTaken = matches.Exists(t => t.Slug == slug && t.ClerkOrganizationId != clerkOrganizationId);
        return new ClerkTenantDirectoryEntry(linked?.Id, linked?.Slug, slugTaken);
    }

    public async Task<ClerkLinkedTenant?> FindTenantAsync(string clerkOrganizationId, CancellationToken cancellationToken)
    {
        await using var context = new ApplicationDbContext(options, new TenantContext());
        return await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.ClerkOrganizationId == clerkOrganizationId)
            .Select(t => new ClerkLinkedTenant(t.Id, t.Slug))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClerkLinkedTenant>> FindTenantsOfUserAsync(string clerkUserId, CancellationToken cancellationToken)
    {
        await using var context = new ApplicationDbContext(options, new TenantContext());
        return await context.TenantMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.ClerkUserId == clerkUserId)
            .Join(context.Tenants.IgnoreQueryFilters(), m => m.TenantId, t => t.Id, (m, t) => new ClerkLinkedTenant(t.Id, t.Slug))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
