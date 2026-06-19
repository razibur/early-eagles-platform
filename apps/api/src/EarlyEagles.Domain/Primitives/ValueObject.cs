namespace EarlyEagles.Domain.Primitives;

/// <summary>
/// Base class for value objects. Equality is structural — two instances of the
/// same concrete type with the same equality components are equal. Subclasses
/// override <see cref="GetEqualityComponents"/> to declare the components.
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }

    public bool Equals(ValueObject? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj)
    {
        return obj is ValueObject value && Equals(value);
    }

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(GetType());
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// Components that define structural equality. Order matters — two value
    /// objects with the same components in different order are not equal.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();
}
