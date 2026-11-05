using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Rsvp"/> to <c>rsvps</c> (P1-001.2). The unique index is what
/// makes "one answer per member per session" true under concurrent requests.
/// There is no foreign key to <c>tenant_members</c>: an answer is club history
/// and outlives the membership.
/// </summary>
public sealed class RsvpConfiguration : IEntityTypeConfiguration<Rsvp>
{
    public void Configure(EntityTypeBuilder<Rsvp> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("rsvps");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Ignore(r => r.DomainEvents);

        builder.Property(r => r.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(r => r.SessionId)
            .HasColumnName("session_id")
            .IsRequired();

        builder.Property(r => r.ClerkUserId)
            .HasColumnName("clerk_user_id")
            .HasMaxLength(TenantMember.ClerkUserIdMaxLength)
            .IsRequired();

        builder.Property(r => r.Response)
            .HasColumnName("response")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(r => new { r.TenantId, r.SessionId, r.ClerkUserId }).IsUnique();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Session>()
            .WithMany()
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
