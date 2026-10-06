namespace CoffeeShop.Domain.Customers;

using CoffeeShop.Domain.Common;

public sealed class LoyaltyTransaction
{
    // Parameterless constructor for EF Core
    private LoyaltyTransaction()
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

        var addsPoints = type is LoyaltyTransactionType.Earn or LoyaltyTransactionType.ReverseRedeem;
        if (addsPoints != (points > 0) || points == int.MinValue)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Points must have the transaction type's sign and support an exact reversal.");
        }

        var isReversal = type is LoyaltyTransactionType.ReverseEarn or LoyaltyTransactionType.ReverseRedeem;
        if (isReversal != (reversesTransactionId is not null))
        {
            throw new ArgumentException("Only reversal entries must reference an original transaction.", nameof(reversesTransactionId));
        }

        if (reversesTransactionId == id)
        {
            throw new ArgumentException("A transaction cannot reverse itself.", nameof(reversesTransactionId));
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

    public EntityId Id { get; private set; } = null!;

    public EntityId CustomerId { get; private set; } = null!;

    public Customer? Customer { get; private set; }

    public EntityId? OrderId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public int Points { get; private set; }

    public int BalanceAfter { get; private set; }

    public EntityId? ReversesTransactionId { get; private set; }

    public LoyaltyTransaction? ReversesTransaction { get; private set; }

    public UtcTimestamp CreatedAt { get; private set; } = null!;

    public EntityId? CreatedBy { get; private set; }
}
