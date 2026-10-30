using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Tenants;

/// <summary>
/// EF Core <see cref="ITenantMemberRepository"/>. Uses the request's context,
/// so the tenant query filter and the write-side stamping interceptor both apply.
/// </summary>
public sealed class EfTenantMemberRepository : ITenantMemberRepository
{
    private readonly ApplicationDbContext context;

    public EfTenantMemberRepository(ApplicationDbContext context)
    {
        this.context = context;
    }

    public Task<TenantMember?> FindAsync(Guid tenantId, string clerkUserId, CancellationToken cancellationToken)
    {
        // Same guard as EfTenantRepository: a context created before the tenant
        // resolved would hide the existing member, and the insert that follows
        // would hit the primary key.
        if (context.CurrentTenantId != tenantId)
        {
            throw new InvalidOperationException(
                $"The request's DbContext was created for tenant '{context.CurrentTenantId}', not '{tenantId}'. " +
                "Resolve the tenant before anything in the request uses ApplicationDbContext.");
        }

        return context.TenantMembers.SingleOrDefaultAsync(m => m.ClerkUserId == clerkUserId, cancellationToken);
    }

    public void Add(TenantMember member) => context.TenantMembers.Add(member);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);
}
