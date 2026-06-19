using EarlyEagles.Domain.Primitives;

namespace EarlyEagles.Tests.Unit.Primitives;

public class ValueObjectTests
{
    [Fact]
    public void Same_Components_Are_Equal()
    {
        var a = new Money(15m, "USD");
        var b = new Money(15m, "USD");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Different_Amount_Is_Not_Equal()
    {
        var a = new Money(15m, "USD");
        var b = new Money(20m, "USD");

        Assert.NotEqual(a, b);
        Assert.False(a == b);
    }

    [Fact]
    public void Different_Currency_Is_Not_Equal()
    {
        var a = new Money(15m, "USD");
        var b = new Money(15m, "EUR");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Component_Order_Matters()
    {
        var a = new Money(15m, "USD");
        var b = new Money.Reversed("USD", 15m);

        ValueObject aBase = a;
        ValueObject bBase = b;

        Assert.NotEqual<ValueObject>(aBase, bBase);
    }

    [Fact]
    public void Different_Concrete_Type_Is_Not_Equal()
    {
        ValueObject a = new Money(15m, "USD");
        ValueObject b = new TaggedMoney(15m, "USD", "dues");

        Assert.NotEqual<ValueObject>(a, b);
    }

    private class Money : ValueObject
    {
        public Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        public decimal Amount { get; }

        public string Currency { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }

        public sealed class Reversed : ValueObject
        {
            public Reversed(string currency, decimal amount)
            {
                Currency = currency;
                Amount = amount;
            }

            public string Currency { get; }

            public decimal Amount { get; }

            protected override IEnumerable<object?> GetEqualityComponents()
            {
                yield return Currency;
                yield return Amount;
            }
        }
    }

    private sealed class TaggedMoney : Money
    {
        public TaggedMoney(decimal amount, string currency, string tag)
            : base(amount, currency)
        {
            Tag = tag;
        }

        public string Tag { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
            yield return Tag;
        }
    }
}
