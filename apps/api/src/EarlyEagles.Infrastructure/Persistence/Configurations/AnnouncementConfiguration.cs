using EarlyEagles.Domain.Announcements;
using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Announcement"/> to <c>announcements</c> (P1-001.4).</summary>
public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("announcements");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Ignore(a => a.DomainEvents);

        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();

        builder.Property(a => a.AuthorClerkUserId)
            .HasColumnName("author_clerk_user_id")
            .HasMaxLength(TenantMember.ClerkUserIdMaxLength)
            .IsRequired();

        builder.Property(a => a.Title)
            .HasColumnName("title")
            .HasMaxLength(Announcement.TitleMaxLength)
            .IsRequired();

        builder.Property(a => a.Body)
            .HasColumnName("body")
            .HasMaxLength(Announcement.BodyMaxLength)
            .IsRequired();

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        // The feed (P1-013) pages a tenant's posts newest first.
        builder.HasIndex(a => new { a.TenantId, a.CreatedAt });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
