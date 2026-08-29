using EarlyEagles.Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EarlyEagles.Infrastructure.Persistence.Interceptors;

/// <summary>
/// The write-side half of tenant isolation. The global query filter in
/// <see cref="ApplicationDbContext"/> guarantees a request can only *read* its own
/// tenant's rows; this interceptor guarantees it can only *write* them. On every
/// <c>SaveChanges</c> it walks the tracked <see cref="ITenantScoped"/> entries and
/// either stamps the ambient tenant onto new rows that have none, or rejects the
/// save outright when the row belongs to a different tenant.
/// </summary>
/// <remarks>
/// <para>Rules, for an entity with its own <c>TenantId</c> column:</para>
/// <list type="bullet">
///   <item>Insert with no tenant id + a resolved tenant → stamped with the ambient tenant.</item>
///   <item>Insert with no tenant id + no resolved tenant → rejected; there is nothing to stamp from.</item>
///   <item>Insert/update/delete carrying a different tenant's id → rejected.</item>
///   <item>Update that changes <c>TenantId</c> → rejected; rows never move between tenants.</item>
///   <item>Any write with an explicit tenant id and no resolved tenant → allowed. This is the
///     write-side twin of <c>IgnoreQueryFilters()</c>: migrations, seeding, provisioning and
///     background jobs run without a request, so they must state the tenant explicitly.</item>
/// </list>
/// <para>
/// The <see cref="EarlyEagles.Domain.Tenants.Tenant"/> aggregate is self-scoped —
/// its tenant id *is* its primary key — so inserts are left alone: creating a
/// tenant is a platform operation that by definition happens before any tenant
/// exists to be ambient. Updates and deletes to it are still constrained to the
/// ambient tenant.
/// </para>
/// <para>
/// Known gap: <c>ExecuteUpdate</c> / <c>ExecuteDelete</c> never reach
/// <c>SaveChanges</c>, so this interceptor does not see them. They do honour the
/// global query filter, so they can only touch the ambient tenant unless the
/// caller adds <c>IgnoreQueryFilters()</c> — which is the same deliberate opt-out
/// the read side already has.
/// </para>
/// <para>
/// Registered as a static, stateless singleton from
/// <c>ApplicationDbContext.OnConfiguring</c> so it applies however the context was
/// built (DI, the design-time factory, or a test constructing it directly) and
/// no caller can forget it. The ambient tenant is read from the context being
/// saved, not held on the interceptor, which keeps a single instance safe to
/// share across every context and tenant.
/// </para>
/// </remarks>
public sealed class TenantStampingInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Enforce(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Enforce(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Enforce(DbContext? context)
    {
        if (context is not ApplicationDbContext applicationContext)
        {
            return;
        }

        var currentTenantId = applicationContext.CurrentTenantId;

        foreach (var entry in applicationContext.ChangeTracker.Entries())
        {
            if (entry.Entity is not ITenantScoped)
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            // Same discriminator resolution as the query filter: a dedicated
            // TenantId column where there is one, the primary key for the
            // self-scoped Tenant aggregate.
            var tenantProperty = entry.Metadata.FindProperty(nameof(ITenantScoped.TenantId));

            if (tenantProperty is null)
            {
                EnforceSelfScoped(entry, currentTenantId);
            }
            else
            {
                EnforceTenantColumn(entry, entry.Property(tenantProperty.Name), currentTenantId);
            }
        }
    }

    private static void EnforceSelfScoped(EntityEntry entry, Guid currentTenantId)
    {
        // Inserting a tenant is provisioning, which runs before that tenant can be
        // ambient — nothing to check. Modifying or deleting one must stay inside
        // the ambient tenant.
        if (entry.State is EntityState.Added || currentTenantId == Guid.Empty)
        {
            return;
        }

        var key = entry.Metadata.FindPrimaryKey();

        if (key is null || key.Properties.Count != 1)
        {
            return;
        }

        if (entry.Property(key.Properties[0].Name).CurrentValue is Guid id && id != currentTenantId)
        {
            throw new CrossTenantWriteException(
                $"{Describe(entry)} targets tenant {id}, but the current tenant is {currentTenantId}.");
        }
    }

    private static void EnforceTenantColumn(EntityEntry entry, PropertyEntry tenantProperty, Guid currentTenantId)
    {
        var assignedTenantId = tenantProperty.CurrentValue is Guid value ? value : Guid.Empty;

        if (entry.State is EntityState.Added && assignedTenantId == Guid.Empty)
        {
            if (currentTenantId == Guid.Empty)
            {
                throw new CrossTenantWriteException(
                    $"{Describe(entry)} has no tenant id and no tenant is resolved for the current " +
                    "request, so there is nothing to stamp it with. Resolve a tenant, or set TenantId " +
                    "explicitly for platform-level work.");
            }

            // The stamp: a handler that forgot the tenant id gets the right one
            // rather than a row in nobody's tenant.
            tenantProperty.CurrentValue = currentTenantId;
            return;
        }

        if (entry.State is EntityState.Modified && tenantProperty.IsModified)
        {
            throw new CrossTenantWriteException(
                $"{Describe(entry)} attempts to change TenantId from " +
                $"{tenantProperty.OriginalValue} to {assignedTenantId}. Rows never move between tenants.");
        }

        if (currentTenantId != Guid.Empty && assignedTenantId != currentTenantId)
        {
            throw new CrossTenantWriteException(
                $"{Describe(entry)} targets tenant {assignedTenantId}, but the current tenant is " +
                $"{currentTenantId}.");
        }
    }

    private static string Describe(EntityEntry entry) =>
        $"{entry.State} {entry.Metadata.ClrType.Name}";
}
