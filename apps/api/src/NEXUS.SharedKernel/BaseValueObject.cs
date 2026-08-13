namespace NEXUS.SharedKernel;

public abstract class BaseValueObject : IEquatable<BaseValueObject>
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public bool Equals(BaseValueObject? other)
    {
        return other is not null && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj)
    {
        return obj is BaseValueObject other && Equals(other);
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents().Select(x => x?.GetHashCode() ?? 0).Aggregate((x, y) => x ^ y);
    }

    public static bool operator ==(BaseValueObject? left, BaseValueObject? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(BaseValueObject? left, BaseValueObject? right)
    {
        return !Equals(left, right);
    }
}
