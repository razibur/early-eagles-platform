using EarlyEagles.Domain.Sessions;
using EarlyEagles.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Session"/> to <c>sessions</c> (P1-001.1). <c>date</c> and
/// <c>start_time</c> are Postgres <c>date</c> / <c>time</c>: the club's local
/// wall clock. The tenant query filter is applied centrally in
/// <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("sessions");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Ignore(s => s.DomainEvents);

        builder.Property(s => s.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(s => s.Date)
            .HasColumnName("date")
            .IsRequired();

        builder.Property(s => s.StartTime)
            .HasColumnName("start_time")
            .IsRequired();

        builder.Property(s => s.Venue)
            .HasColumnName("venue")
            .HasMaxLength(Session.VenueMaxLength)
            .IsRequired();

        builder.Property(s => s.FeeCents)
            .HasColumnName("fee_cents")
            .IsRequired();

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // The week view (P1-005) reads a tenant's sessions by date range.
        builder.HasIndex(s => new { s.TenantId, s.Date });

        // Sessions are club history (attendance, payments); keep them if a
        // tenant row is ever removed by hand. Tenants are soft-deleted.
        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
