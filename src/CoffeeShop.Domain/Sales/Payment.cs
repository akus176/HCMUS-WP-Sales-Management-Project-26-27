namespace CoffeeShop.Domain.Sales;

using CoffeeShop.Domain.Common;

public class Payment
{
    protected Payment()
    {
    }

    public EntityId Id { get; protected set; } = null!;
    public EntityId OrderId { get; protected set; } = null!;
    public string Method { get; protected set; } = string.Empty;
    public Money Amount { get; protected set; } = Money.Zero;
    public string Status { get; protected set; } = "succeeded";
    public string? Reference { get; protected set; }
    public UtcTimestamp PaidAt { get; protected set; } = null!;
    public EntityId RecordedBy { get; protected set; } = null!;
}
