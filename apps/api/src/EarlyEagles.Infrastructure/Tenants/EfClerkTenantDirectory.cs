using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Tenants;

/// <summary>
/// EF Core <see cref="IClerkTenantDirectory"/>. Platform work: it runs before
/// any tenant is resolved, so it bypasses the query filter, and it projects ids
/// and slugs only.
/// </summary>
public sealed class EfClerkTenantDirectory : IClerkTenantDirectory
{
    private readonly ApplicationDbContext context;

    public EfClerkTenantDirectory(ApplicationDbContext context)
    {
        this.context = context;
    }

    public async Task<ClerkTenantDirectoryEntry> FindAsync(string clerkOrganizationId, string slug, CancellationToken cancellationToken)
    {
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
}
