using EarlyEagles.Domain.Payments;
using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="SessionPayment"/> to <c>session_payments</c> (P1-001.3). The
/// unique index keeps one payment per member per session under concurrent
/// reports. No foreign key to <c>tenant_members</c>: the ledger outlives the
/// membership.
/// </summary>
public sealed class SessionPaymentConfiguration : IEntityTypeConfiguration<SessionPayment>
{
    public void Configure(EntityTypeBuilder<SessionPayment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("session_payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Ignore(p => p.DomainEvents);

        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.SessionId).HasColumnName("session_id").IsRequired();

        builder.Property(p => p.ClerkUserId)
            .HasColumnName("clerk_user_id")
            .HasMaxLength(TenantMember.ClerkUserIdMaxLength)
            .IsRequired();

        builder.Property(p => p.AmountCents).HasColumnName("amount_cents").IsRequired();

        builder.Property(p => p.SenderName)
            .HasColumnName("sender_name")
            .HasMaxLength(SessionPayment.SenderNameMaxLength)
            .IsRequired();

        builder.Property(p => p.Reference)
            .HasColumnName("reference")
            .HasMaxLength(SessionPayment.ReferenceMaxLength)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(p => p.ReportedAt).HasColumnName("reported_at").IsRequired();

        builder.Property(p => p.ReviewedBy)
            .HasColumnName("reviewed_by")
            .HasMaxLength(TenantMember.ClerkUserIdMaxLength);

        builder.Property(p => p.ReviewedAt).HasColumnName("reviewed_at");

        builder.Property(p => p.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(SessionPayment.RejectionReasonMaxLength);

        builder.HasIndex(p => new { p.TenantId, p.SessionId, p.ClerkUserId }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(p => p.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
