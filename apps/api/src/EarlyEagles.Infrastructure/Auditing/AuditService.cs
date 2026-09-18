using EarlyEagles.Application.Abstractions.Auditing;
using EarlyEagles.Application.Abstractions.Tenancy;
using EarlyEagles.Domain.Auditing;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Infrastructure.Auditing;

/// <summary>
/// Persists <see cref="AuditEntry"/> rows to the append-only
/// <c>audit_events</c> table, enriching each row with the ambient tenant and
/// timestamp. Called from <c>AuditBehavior</c> (Application layer) after every
/// command dispatch, both success and failure.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="LogAsync"/> call opens its own fresh <see cref="ApplicationDbContext"/>.
/// That is the whole point of the audit log: it must persist *independently* of
/// the request's transaction, so a rolled-back command still leaves a trail
/// showing what was attempted and why it failed. A fresh context gets a fresh
/// connection from the Npgsql pool with no ambient transaction, so its
/// <c>SaveChanges</c> auto-commits regardless of the outer request's outcome.
/// </para>
/// <para>
/// The fresh context is constructed with an unresolved
/// <see cref="TenantContext"/>. The audit row carries an explicit
/// <see cref="AuditEvent.TenantId"/> read from the ambient
/// <see cref="ITenantContext"/> (which must be resolved — commands only run
/// inside a tenant-scoped request), so the write-side tenant-stamping
/// interceptor's "explicit tenant + no ambient" allowance applies (P0-034.4,
/// the platform-work escape hatch).
/// </para>
/// <para>
/// Actor enrichment is deferred: once <c>ICurrentUser</c> lands with the Clerk
/// integration (P0-035), it becomes a constructor dependency and populates
/// <see cref="AuditEvent.ActorClerkUserId"/>. Until then, all rows record
/// <c>null</c>, which the aggregate already accepts as "system-originated".
/// </para>
/// </remarks>
public sealed class AuditService : IAuditService
{
    private readonly DbContextOptions<ApplicationDbContext> options;
    private readonly ITenantContext tenantContext;

    public AuditService(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
    {
        this.options = options;
        this.tenantContext = tenantContext;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!tenantContext.IsResolved)
        {
            // The audit log is per-tenant; a row with no tenant is nonsense. If
            // this fires, either AuditBehavior is running outside a tenant-scoped
            // request or the tenant-resolution middleware is misconfigured — both
            // are bugs to fix upstream, not conditions to swallow.
            throw new InvalidOperationException(
                "Cannot record an audit event without a resolved tenant. AuditBehavior is intended to run inside tenant-scoped requests only.");
        }

        var auditEvent = AuditEvent.Create(
            id: Guid.NewGuid(),
            tenantId: tenantContext.TenantId,
            action: entry.Action,
            success: entry.Success,
            failureReason: entry.FailureReason,
            actorClerkUserId: null,
            occurredAt: DateTimeOffset.UtcNow);

        // Fresh context per call. Unresolved tenant is deliberate: the row
        // carries the tenant explicitly and the write-side interceptor's
        // platform-work branch allows the write.
        await using var context = new ApplicationDbContext(options, new TenantContext());
        context.AuditEvents.Add(auditEvent);
        await context.SaveChangesAsync(cancellationToken);
    }
}
