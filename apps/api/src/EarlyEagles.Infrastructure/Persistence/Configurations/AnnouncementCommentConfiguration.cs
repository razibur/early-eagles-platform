using EarlyEagles.Domain.Announcements;
using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AnnouncementComment"/> to <c>announcement_comments</c> (P1-001.4).</summary>
public sealed class AnnouncementCommentConfiguration : IEntityTypeConfiguration<AnnouncementComment>
{
    public void Configure(EntityTypeBuilder<AnnouncementComment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("announcement_comments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Ignore(c => c.DomainEvents);

        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.AnnouncementId).HasColumnName("announcement_id").IsRequired();

        builder.Property(c => c.AuthorClerkUserId)
            .HasColumnName("author_clerk_user_id")
            .HasMaxLength(TenantMember.ClerkUserIdMaxLength)
            .IsRequired();

        builder.Property(c => c.Body)
            .HasColumnName("body")
            .HasMaxLength(AnnouncementComment.BodyMaxLength)
            .IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(c => new { c.TenantId, c.AnnouncementId, c.CreatedAt });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Announcement>()
            .WithMany()
            .HasForeignKey(c => c.AnnouncementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
