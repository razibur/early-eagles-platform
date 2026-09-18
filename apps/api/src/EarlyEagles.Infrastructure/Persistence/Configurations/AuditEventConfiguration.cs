using EarlyEagles.Domain.Auditing;
using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="AuditEvent"/> to the append-only <c>audit_events</c> table.
/// The type has no updater methods, but the Postgres trigger installed by the
/// migration (P0-046.2) is the storage-layer backstop against
/// <c>ExecuteUpdate</c> / <c>ExecuteDelete</c> and any hand-crafted SQL. The
/// tenant query filter is applied centrally in <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("audit_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        // Domain-event buffer from AggregateRoot is dispatched, never persisted.
        builder.Ignore(e => e.DomainEvents);

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(e => e.Action)
            .HasColumnName("action")
            .HasMaxLength(AuditEvent.ActionMaxLength)
            .IsRequired();

        builder.Property(e => e.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(e => e.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(AuditEvent.FailureReasonMaxLength);

        builder.Property(e => e.ActorClerkUserId)
            .HasColumnName("actor_clerk_user_id")
            .HasMaxLength(AuditEvent.ActorClerkUserIdMaxLength);

        builder.Property(e => e.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        // The overwhelming access pattern is "the tenant's recent audit rows,
        // newest first" — for admin views, exports, incident review. A composite
        // index on (tenant_id, occurred_at DESC) serves that query directly.
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt })
            .IsDescending(false, true);

        // Preserve audit history when a tenant row goes away — the log is the
        // durable record of what happened. Tenants are soft-deleted in normal
        // operation (Status = Deactivated) so this restriction is a defensive
        // fence rather than an active constraint.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
