namespace CoffeeShop.Domain.Customers;

using CoffeeShop.Domain.Common;

public class Customer
{
    private readonly List<CustomerAddress> _addresses = [];
    private readonly List<LoyaltyTransaction> _loyaltyTransactions = [];

    // Parameterless constructor for EF Core
    protected Customer()
    {
    }

    public Customer(
        EntityId id,
        string fullName,
        string phone,
        UtcTimestamp createdAt,
        UtcTimestamp updatedAt,
        string? email = null,
        bool isActive = true,
        int loyaltyPointsBalance = 0)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        if (loyaltyPointsBalance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(loyaltyPointsBalance), "Loyalty points balance cannot be negative.");
        }

        Id = id;
        FullName = fullName.Trim();
        Phone = phone.Trim();
        Email = email?.Trim().ToLowerInvariant();
        IsActive = isActive;
        LoyaltyPointsBalance = loyaltyPointsBalance;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public EntityId Id { get; protected set; } = null!;

    public string FullName { get; protected set; } = string.Empty;

    public string Phone { get; protected set; } = string.Empty;

    public string? Email { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    public int LoyaltyPointsBalance { get; protected set; }

    public UtcTimestamp CreatedAt { get; protected set; } = null!;

    public UtcTimestamp UpdatedAt { get; protected set; } = null!;

    public IReadOnlyCollection<CustomerAddress> Addresses => _addresses.AsReadOnly();

    public IReadOnlyCollection<LoyaltyTransaction> LoyaltyTransactions => _loyaltyTransactions.AsReadOnly();
}
