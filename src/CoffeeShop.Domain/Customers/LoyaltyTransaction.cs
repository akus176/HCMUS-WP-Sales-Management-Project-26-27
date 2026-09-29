namespace CoffeeShop.Domain.Customers;

using CoffeeShop.Domain.Common;

public class LoyaltyTransaction
{
    // Parameterless constructor for EF Core
    protected LoyaltyTransaction()
    {
    }

    public LoyaltyTransaction(
        EntityId id,
        EntityId customerId,
        string type,
        int points,
        int balanceAfter,
        UtcTimestamp createdAt,
        EntityId? orderId = null,
        EntityId? reversesTransactionId = null,
        EntityId? createdBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        if (!LoyaltyTransactionType.IsValid(type))
        {
            throw new ArgumentException($"Invalid loyalty transaction type: {type}.", nameof(type));
        }

        if (points == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Loyalty points delta cannot be zero.");
        }

        if (balanceAfter < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balanceAfter), "Balance after loyalty transaction cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(createdAt);

        Id = id;
        CustomerId = customerId;
        Type = type;
        Points = points;
        BalanceAfter = balanceAfter;
        CreatedAt = createdAt;
        OrderId = orderId;
        ReversesTransactionId = reversesTransactionId;
        CreatedBy = createdBy;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId CustomerId { get; protected set; } = null!;

    public Customer? Customer { get; protected set; }

    public EntityId? OrderId { get; protected set; }

    public string Type { get; protected set; } = string.Empty;

    public int Points { get; protected set; }

    public int BalanceAfter { get; protected set; }

    public EntityId? ReversesTransactionId { get; protected set; }

    public LoyaltyTransaction? ReversesTransaction { get; protected set; }

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public EntityId? CreatedBy { get; protected set; }
}
