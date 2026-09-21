using EarlyEagles.Application.Abstractions.Messaging;
using EarlyEagles.Domain.Primitives;
using EarlyEagles.Infrastructure.Persistence.Interceptors;

namespace EarlyEagles.Tests.Unit.Outbox;

// P0-049.2 — the drain itself, exercised against a recording dispatcher so the
// rules are pinned without a database: which tenant a row is attributed to, that
// buffers are emptied exactly once, and that an untenanted event is refused
// rather than filed under nobody. The EF plumbing that feeds it is covered by
// the integration tests.
public class DomainEventOutboxInterceptorTests
{
    private static readonly Guid AmbientTenantId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OwnTenantId = new("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Aggregate_Without_Events_Is_Skipped()
    {
        var dispatcher = new RecordingDispatcher();

        DomainEventOutboxInterceptor.Stage([new FakeAggregate(OwnTenantId)], AmbientTenantId, dispatcher);

        Assert.Empty(dispatcher.Dispatches);
    }

    [Fact]
    public void Events_Are_Dispatched_And_The_Buffer_Is_Emptied()
    {
        var aggregate = new FakeAggregate(OwnTenantId, new FakeEvent(), new FakeEvent());
        var dispatcher = new RecordingDispatcher();

        DomainEventOutboxInterceptor.Stage([aggregate], AmbientTenantId, dispatcher);

        var dispatch = Assert.Single(dispatcher.Dispatches);
        Assert.Equal(2, dispatch.Events.Count);

        // Clearing is what keeps a second SaveChanges on the same tracked graph
        // from queueing the same events again.
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void The_Aggregates_Own_Tenant_Wins_Over_The_Ambient_One()
    {
        var aggregate = new FakeAggregate(OwnTenantId, new FakeEvent());
        var dispatcher = new RecordingDispatcher();

        DomainEventOutboxInterceptor.Stage([aggregate], AmbientTenantId, dispatcher);

        Assert.Equal(OwnTenantId, Assert.Single(dispatcher.Dispatches).TenantId);
    }

    [Fact]
    public void Ambient_Tenant_Covers_An_Aggregate_That_Has_None_Yet()
    {
        // The insert the tenant-stamping interceptor is about to stamp: the row
        // has no tenant id of its own, so the outbox row takes the same fallback.
        var aggregate = new FakeAggregate(Guid.Empty, new FakeEvent());
        var dispatcher = new RecordingDispatcher();

        DomainEventOutboxInterceptor.Stage([aggregate], AmbientTenantId, dispatcher);

        Assert.Equal(AmbientTenantId, Assert.Single(dispatcher.Dispatches).TenantId);
    }

    [Fact]
    public void An_Aggregate_With_No_Tenant_At_All_Is_Refused()
    {
        var aggregate = new FakeAggregate(Guid.Empty, new FakeEvent());
        var dispatcher = new RecordingDispatcher();

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            DomainEventOutboxInterceptor.Stage([aggregate], Guid.Empty, dispatcher));

        Assert.Contains(nameof(FakeAggregate), thrown.Message, StringComparison.Ordinal);
        Assert.Empty(dispatcher.Dispatches);

        // The buffer survives the refusal, so nothing is silently dropped.
        Assert.Single(aggregate.DomainEvents);
    }

    [Fact]
    public void Untenanted_Aggregates_Fall_Back_To_The_Ambient_Tenant()
    {
        // Not every aggregate is ITenantScoped — a platform-level one still gets
        // its events queued under the tenant whose request raised them.
        var aggregate = new UntenantedAggregate(new FakeEvent());
        var dispatcher = new RecordingDispatcher();

        DomainEventOutboxInterceptor.Stage([aggregate], AmbientTenantId, dispatcher);

        Assert.Equal(AmbientTenantId, Assert.Single(dispatcher.Dispatches).TenantId);
    }

    private sealed record FakeEvent : IDomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();

        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }

    private sealed class FakeAggregate : IHasDomainEvents, ITenantScoped
    {
        private readonly List<IDomainEvent> events;

        public FakeAggregate(Guid tenantId, params IDomainEvent[] events)
        {
            TenantId = tenantId;
            this.events = [.. events];
        }

        public Guid TenantId { get; }

        public IReadOnlyCollection<IDomainEvent> DomainEvents => events.AsReadOnly();

        public void ClearDomainEvents() => events.Clear();
    }

    private sealed class UntenantedAggregate : IHasDomainEvents
    {
        private readonly List<IDomainEvent> events;

        public UntenantedAggregate(params IDomainEvent[] events) => this.events = [.. events];

        public IReadOnlyCollection<IDomainEvent> DomainEvents => events.AsReadOnly();

        public void ClearDomainEvents() => events.Clear();
    }

    private sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<(Guid TenantId, IReadOnlyCollection<IDomainEvent> Events)> Dispatches { get; } = [];

        public void Dispatch(Guid tenantId, IReadOnlyCollection<IDomainEvent> domainEvents) =>
            Dispatches.Add((tenantId, domainEvents));
    }
}
