using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="TenantMember"/> to the <c>tenant_members</c> table. The
/// primary key is the two scalar columns <c>tenant_id</c> + <c>clerk_user_id</c>
/// (the value-object <c>Id</c> is ignored — it is a view over them).
/// <c>tenant_id</c> also carries the FK to <c>tenants</c>. The tenant query filter
/// is applied centrally in <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class TenantMemberConfiguration : IEntityTypeConfiguration<TenantMember>
{
    public void Configure(EntityTypeBuilder<TenantMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tenant_members");

        // The composite scalar key is the source of truth; the TenantMemberId
        // value object is reconstructed by the (EF-bound) constructor.
        builder.Ignore(m => m.Id);
        builder.HasKey(m => new { m.TenantId, m.ClerkUserId });

        builder.Property(m => m.TenantId).HasColumnName("tenant_id");

        builder.Property(m => m.ClerkUserId)
            .HasColumnName("clerk_user_id")
            .HasMaxLength(TenantMember.ClerkUserIdMaxLength)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(m => m.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
