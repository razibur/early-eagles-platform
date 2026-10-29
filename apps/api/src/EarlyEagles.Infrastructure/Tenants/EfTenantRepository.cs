using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Tenants;

/// <summary>
/// EF Core <see cref="ITenantRepository"/>. Uses the request's context, so the
/// tenant query filter and the write-side stamping interceptor both apply.
/// </summary>
public sealed class EfTenantRepository : ITenantRepository
{
    private readonly ApplicationDbContext context;

    public EfTenantRepository(ApplicationDbContext context)
    {
        this.context = context;
    }

    public Task<Tenant?> FindAsync(Guid tenantId, CancellationToken cancellationToken) =>
        context.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

    public void Add(Tenant tenant) => context.Tenants.Add(tenant);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
