using EarlyEagles.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyEagles.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="OutboxMessage"/> to the <c>outbox_messages</c> table. The
/// primary access pattern is the background publisher polling for pending
/// rows, so a partial index on unprocessed rows serves that scan directly and
/// stays tiny (the vast majority of rows over the lifetime of the platform are
/// processed and irrelevant to the poller). The tenant query filter is applied
/// centrally in <see cref="ApplicationDbContext"/>.
/// </summary>
public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(OutboxMessage.EventTypeMaxLength)
            .IsRequired();

        // JSON payload — no length cap (bodies can be arbitrarily large) and
        // stored as text rather than jsonb to keep this table framework-agnostic;
        // the publisher shapes the message, the outbox just carries the bytes.
        builder.Property(m => m.Payload)
            .HasColumnName("payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(m => m.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.Property(m => m.ProcessedAt).HasColumnName("processed_at");

        builder.Property(m => m.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(m => m.LastError)
            .HasColumnName("last_error")
            .HasColumnType("text");

        // Partial index — the poller only cares about unprocessed rows, and
        // processed rows dominate the table over time. Keeps the working-set
        // scan tiny regardless of history size.
        builder.HasIndex(m => m.OccurredAt)
            .HasDatabaseName("IX_outbox_messages_pending")
            .HasFilter("\"processed_at\" IS NULL");
    }
}
