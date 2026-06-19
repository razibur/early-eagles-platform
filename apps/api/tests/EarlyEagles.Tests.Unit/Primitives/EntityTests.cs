using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Tests.Unit.Primitives;

public class EntityTests
{
    [Fact]
    public void Two_Entities_With_Same_Id_And_Type_Are_Equal()
    {
        var id = Guid.NewGuid();
        var a = new SampleEntity(id);
        var b = new SampleEntity(id);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Two_Entities_With_Different_Ids_Are_Not_Equal()
    {
        var a = new SampleEntity(Guid.NewGuid());
        var b = new SampleEntity(Guid.NewGuid());

        Assert.NotEqual(a, b);
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Same_Id_But_Different_Concrete_Type_Are_Not_Equal()
    {
        var id = Guid.NewGuid();
        var a = new SampleEntity(id);
        var b = new OtherEntity(id);

        // Cast both to the common base so the operator overload binds at the base level.
        Entity<Guid> aBase = a;
        Entity<Guid> bBase = b;

        Assert.NotEqual<Entity<Guid>>(aBase, bBase);
        Assert.False(aBase == bBase);
    }

    [Fact]
    public void Equals_Null_Returns_False()
    {
        var a = new SampleEntity(Guid.NewGuid());

        Assert.False(a.Equals((Entity<Guid>?)null));
        Assert.False(a.Equals((object?)null));
    }

    [Fact]
    public void Two_Null_References_Are_Equal_Via_Operator()
    {
        SampleEntity? a = null;
        SampleEntity? b = null;

        Assert.True(a == b);
        Assert.False(a != b);
    }

    private sealed class SampleEntity : Entity<Guid>
    {
        public SampleEntity(Guid id)
            : base(id)
        {
        }
    }

    private sealed class OtherEntity : Entity<Guid>
    {
        public OtherEntity(Guid id)
            : base(id)
        {
        }
    }
}
