using EarlyEagles.Domain.Notifications;
using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="PushSubscription"/> to <c>push_subscriptions</c> (P0-068.3).
/// One row per browser per tenant: <c>(tenant_id, endpoint)</c> is unique. The
/// tenant query filter is applied centrally in <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("push_subscriptions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.ClerkUserId)
            .HasColumnName("clerk_user_id")
            .HasMaxLength(TenantMember.ClerkUserIdMaxLength)
            .IsRequired();

        builder.Property(s => s.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(PushSubscription.EndpointMaxLength)
            .IsRequired();

        builder.Property(s => s.P256dh)
            .HasColumnName("p256dh")
            .HasMaxLength(PushSubscription.KeyMaxLength)
            .IsRequired();

        builder.Property(s => s.Auth)
            .HasColumnName("auth")
            .HasMaxLength(PushSubscription.KeyMaxLength)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // A browser subscribes once per club; subscribing again renews the row.
        builder.HasIndex(s => new { s.TenantId, s.Endpoint }).IsUnique();

        // Sending looks up a member's devices in the club.
        builder.HasIndex(s => new { s.TenantId, s.ClerkUserId });

        // A deleted tenant's subscriptions are worthless.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
