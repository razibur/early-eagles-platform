using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Tenant"/> aggregate to the <c>tenants</c> table. Enums are
/// stored as strings so the on-disk value survives enum reordering; the branding
/// value object is an optional owned type. The tenant self-scoping query filter
/// (a tenant sees only its own row) is applied centrally in
/// <see cref="ApplicationDbContext"/>, not here.
/// </summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("tenants");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id").ValueGeneratedNever();

        // TenantId is the computed self-reference (== Id); it is not its own column.
        builder.Ignore(t => t.TenantId);

        // Domain-event buffer from AggregateRoot is dispatched, never persisted.
        builder.Ignore(t => t.DomainEvents);

        builder.Property(t => t.Slug)
            .HasColumnName("slug")
            .HasMaxLength(Tenant.SlugMaxLength)
            .IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(Tenant.DisplayNameMaxLength)
            .IsRequired();

        builder.Property(t => t.Plan)
            .HasColumnName("plan")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Clerk org = tenant (P0-036.2). Nullable: seeded and test tenants have
        // none. Unique so one organization can never provision two tenants;
        // Postgres allows any number of NULLs in a unique index.
        builder.Property(t => t.ClerkOrganizationId)
            .HasColumnName("clerk_organization_id")
            .HasMaxLength(Tenant.ClerkOrganizationIdMaxLength);
        builder.HasIndex(t => t.ClerkOrganizationId).IsUnique();

        builder.OwnsOne(t => t.Branding, branding =>
        {
            branding.Property(b => b.LogoUrl)
                .HasColumnName("branding_logo_url")
                .HasMaxLength(Branding.LogoUrlMaxLength);

            // Hex colours are at most "#RRGGBBAA" (9 chars).
            branding.Property(b => b.PrimaryColor)
                .HasColumnName("branding_primary_color")
                .HasMaxLength(9);

            branding.Property(b => b.AccentColor)
                .HasColumnName("branding_accent_color")
                .HasMaxLength(9);
        });
        builder.Navigation(t => t.Branding).IsRequired(false);
    }
}
