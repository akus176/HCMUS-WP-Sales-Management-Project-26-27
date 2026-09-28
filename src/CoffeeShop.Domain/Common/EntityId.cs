namespace CoffeeShop.Domain.Common;

public sealed record EntityId
{
    public EntityId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Entity identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static EntityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
