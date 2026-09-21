using EarlyEagles.Application.Abstractions.Messaging;
using EarlyEagles.Domain.Primitives;
using EarlyEagles.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EarlyEagles.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Drains the domain events buffered on every tracked aggregate into the outbox
/// on the way into <c>SaveChanges</c> (P0-049.2). Because the rows are added to
/// the same change tracker that is already saving, they are written by the same
/// statement batch inside the same transaction as the state change — atomicity
/// with no per-handler code.
/// </summary>
/// <remarks>
/// <para>
/// Registered from <see cref="ApplicationDbContext.OnConfiguring"/>, ahead of
/// <see cref="TenantStampingInterceptor"/>, so the rows it stages are still
/// subject to the write-side tenant guard rather than slipping in behind it.
/// Like that interceptor it is a stateless static singleton: everything it needs
/// comes from the context being saved, which also means it applies however the
/// context was built (DI, the design-time factory, or a test constructing one).
/// </para>
/// <para>
/// It builds its <see cref="OutboxDomainEventDispatcher"/> over that same
/// context rather than resolving the DI-scoped one. The context being saved is
/// not always the request's — <c>AuditService</c> opens its own — and staging on
/// anything but the saving context would put the rows in a different transaction,
/// which is precisely the bug this task exists to prevent.
/// </para>
/// <para>
/// Events are cleared from the aggregate once staged, so a second
/// <c>SaveChanges</c> on the same tracked graph does not queue them twice. They
/// are cleared in memory only: if the transaction rolls back, the outbox rows go
/// with it and the events are gone too — correct, because the state change they
/// describe never happened.
/// </para>
/// </remarks>
public sealed class DomainEventOutboxInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Stages every aggregate's buffered events through
    /// <paramref name="dispatcher"/> and empties the buffers.
    /// <paramref name="ambientTenantId"/> is the tenant of the request being
    /// served, used only for aggregates that do not yet carry one of their own
    /// (the same fallback the tenant-stamping interceptor applies to inserts).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// An aggregate raised events but neither it nor the request names a tenant,
    /// so the outbox row would belong to nobody.
    /// </exception>
    public static void Stage(
        IEnumerable<IHasDomainEvents> aggregates,
        Guid ambientTenantId,
        IDomainEventDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(aggregates);
        ArgumentNullException.ThrowIfNull(dispatcher);

        foreach (var aggregate in aggregates)
        {
            // Snapshot before dispatching: DomainEvents is a read-only view over
            // the live list that ClearDomainEvents empties.
            var domainEvents = aggregate.DomainEvents.ToArray();

            if (domainEvents.Length == 0)
            {
                continue;
            }

            dispatcher.Dispatch(ResolveTenantId(aggregate, ambientTenantId), domainEvents);
            aggregate.ClearDomainEvents();
        }
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Stage(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Stage(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Stage(DbContext? context)
    {
        if (context is not ApplicationDbContext applicationContext)
        {
            return;
        }

        // Materialised before dispatching, because dispatching adds outbox
        // entries to the very change tracker being enumerated.
        var aggregates = applicationContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        Stage(aggregates, applicationContext.CurrentTenantId, new OutboxDomainEventDispatcher(applicationContext));
    }

    private static Guid ResolveTenantId(IHasDomainEvents aggregate, Guid ambientTenantId)
    {
        // The aggregate's own tenant is the truth where it has one. The ambient
        // tenant covers the insert that has not been stamped yet, and platform
        // work (provisioning a tenant) has no ambient tenant but a self-scoped
        // aggregate that knows its own id.
        var tenantId = aggregate is ITenantScoped scoped ? scoped.TenantId : Guid.Empty;

        if (tenantId == Guid.Empty)
        {
            tenantId = ambientTenantId;
        }

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"{aggregate.GetType().Name} raised domain events but carries no tenant id and no tenant is " +
                "resolved for the current request, so the outbox rows would belong to no tenant. Resolve a " +
                "tenant, or set TenantId explicitly for platform-level work.");
        }

        return tenantId;
    }
}
