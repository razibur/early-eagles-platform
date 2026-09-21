using EarlyEagles.Domain.Primitives;
using EarlyEagles.Infrastructure.Outbox;
using EarlyEagles.Infrastructure.Persistence;
using EarlyEagles.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EarlyEagles.Tests.Unit.Outbox;

// P0-049.2 — how an event becomes a row. Staging only touches the change
// tracker, so these run without a database: no connection is opened until
// something calls SaveChanges, which is exactly the point — the rows ride along
// with whatever transaction the caller already has open.
public class OutboxDomainEventDispatcherTests
{
    private static readonly Guid TenantId = new("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void An_Event_Becomes_A_Pending_Outbox_Row()
    {
        using var context = NewContext();
        var dispatcher = new OutboxDomainEventDispatcher(context);
        var raised = new SampleEvent("alpha");

        dispatcher.Dispatch(TenantId, [raised]);

        var staged = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>());
        Assert.Equal(EntityState.Added, staged.State);

        var message = staged.Entity;
        Assert.Equal(raised.Id, message.Id);
        Assert.Equal(TenantId, message.TenantId);
        Assert.Equal(typeof(SampleEvent).FullName, message.EventType);
        Assert.Equal(raised.OccurredAt, message.OccurredAt);

        // Pending until the P0-050 poller publishes it.
        Assert.Null(message.ProcessedAt);
        Assert.Equal(0, message.AttemptCount);
    }

    [Fact]
    public void The_Payload_Is_The_Runtime_Type_Serialised_As_Web_Json()
    {
        // Serialising against IDomainEvent instead of the concrete type would
        // silently drop every property the event actually carries.
        using var context = NewContext();

        new OutboxDomainEventDispatcher(context).Dispatch(TenantId, [new SampleEvent("alpha")]);

        var payload = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>()).Entity.Payload;
        Assert.Contains("\"slug\":\"alpha\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Staging_Opens_No_Transaction_And_Saves_Nothing()
    {
        // The dispatcher must never save on its own: doing so would commit the
        // event outside the caller's transaction and resurrect the dual-write
        // problem the outbox exists to remove.
        using var context = NewContext();

        new OutboxDomainEventDispatcher(context).Dispatch(TenantId, [new SampleEvent("alpha")]);

        Assert.Null(context.Database.CurrentTransaction);
        Assert.True(context.ChangeTracker.HasChanges());
    }

    [Fact]
    public void A_Non_Utc_Timestamp_Is_Normalised()
    {
        // OutboxMessage.Create rejects a non-zero offset outright, so the
        // dispatcher has to convert rather than pass it through.
        using var context = NewContext();
        var occurredAt = new DateTimeOffset(2026, 9, 21, 9, 30, 0, TimeSpan.FromHours(-4));

        new OutboxDomainEventDispatcher(context)
            .Dispatch(TenantId, [new SampleEvent("alpha") { OccurredAt = occurredAt }]);

        var message = Assert.Single(context.ChangeTracker.Entries<OutboxMessage>()).Entity;
        Assert.Equal(TimeSpan.Zero, message.OccurredAt.Offset);
        Assert.Equal(occurredAt.UtcDateTime, message.OccurredAt.UtcDateTime);
    }

    [Fact]
    public void Every_Event_In_The_Batch_Gets_Its_Own_Row()
    {
        using var context = NewContext();

        new OutboxDomainEventDispatcher(context)
            .Dispatch(TenantId, [new SampleEvent("alpha"), new SampleEvent("beta")]);

        Assert.Equal(2, context.ChangeTracker.Entries<OutboxMessage>().Count());
    }

    // No connection is ever opened — building the model and tracking entities
    // needs a provider, not a server.
    private static ApplicationDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;

        return new ApplicationDbContext(options, new TenantContext());
    }

    private sealed record SampleEvent(string Slug) : IDomainEvent
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
