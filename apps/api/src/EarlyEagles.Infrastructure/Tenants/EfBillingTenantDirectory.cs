using System.Linq.Expressions;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Tenants;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Tenants;

/// <summary>
/// EF Core <see cref="IBillingTenantDirectory"/> (P0-043.2). Platform work,
/// like <see cref="EfClerkTenantDirectory"/>: it bypasses the query filter and
/// opens its own short-lived context, so the request's context isn't created
/// (and pinned to "no tenant") before the webhook sets the tenant.
/// </summary>
public sealed class EfBillingTenantDirectory : IBillingTenantDirectory
{
    private readonly DbContextOptions<ApplicationDbContext> options;

    public EfBillingTenantDirectory(DbContextOptions<ApplicationDbContext> options)
    {
        this.options = options;
    }

    public Task<BillingTenant?> FindAsync(Guid tenantId, CancellationToken cancellationToken) =>
        FindSingleAsync(t => t.Id == tenantId, cancellationToken);

    // stripe_customer_id is unique (P0-045.1), so at most one row matches.
    public Task<BillingTenant?> FindByStripeCustomerAsync(string stripeCustomerId, CancellationToken cancellationToken) =>
        FindSingleAsync(t => t.StripeCustomerId == stripeCustomerId, cancellationToken);

    private async Task<BillingTenant?> FindSingleAsync(Expression<Func<Tenant, bool>> predicate, CancellationToken cancellationToken)
    {
        await using var context = new ApplicationDbContext(options, new TenantContext());
        return await context.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(predicate)
            .Select(t => new BillingTenant(t.Id, t.Slug, t.Status, t.StripeCustomerId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
