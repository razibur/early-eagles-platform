using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Tests.Unit.Primitives;

public class AggregateRootTests
{
    [Fact]
    public void New_Aggregate_Has_No_Domain_Events()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Raise_Domain_Event_Appends_To_Collection()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        var first = new SampleEvent();
        var second = new SampleEvent();

        aggregate.RaiseSample(first);
        aggregate.RaiseSample(second);

        Assert.Collection(
            aggregate.DomainEvents,
            e => Assert.Same(first, e),
            e => Assert.Same(second, e));
    }

    [Fact]
    public void Clear_Domain_Events_Empties_The_Collection()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        aggregate.RaiseSample(new SampleEvent());

        aggregate.ClearDomainEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void Raise_Domain_Event_Rejects_Null()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());

        Assert.Throws<ArgumentNullException>(() => aggregate.RaiseSample(null!));
    }

    private sealed class SampleAggregate : AggregateRoot<Guid>
    {
        public SampleAggregate(Guid id)
            : base(id)
        {
        }

        public void RaiseSample(IDomainEvent domainEvent)
        {
            RaiseDomainEvent(domainEvent);
        }
    }

    private sealed record SampleEvent : IDomainEvent
    {
        public Guid Id { get; } = Guid.NewGuid();

        public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
    }
}
