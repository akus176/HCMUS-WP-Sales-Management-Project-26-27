namespace CoffeeShop.Domain.Customers;

using System.Text.RegularExpressions;
using CoffeeShop.Domain.Common;

public class Customer
{
    private static readonly Regex PhoneRegex = new(@"^\+?[0-9\s\-()]{7,30}$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

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
        ArgumentNullException.ThrowIfNull(createdAt);
        ArgumentNullException.ThrowIfNull(updatedAt);

        if (loyaltyPointsBalance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(loyaltyPointsBalance), "Loyalty points balance cannot be negative.");
        }

        Id = id;
        FullName = ValidateAndNormalizeFullName(fullName);
        Phone = ValidateAndNormalizePhone(phone);
        Email = ValidateAndNormalizeEmail(email);
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

    /// <summary>
    /// Updates the customer's profile contact details. Preserves active status:
    /// updating does not automatically reactivate a deactivated customer.
    /// </summary>
    public void Update(string fullName, string phone, string? email, UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);

        var validatedFullName = ValidateAndNormalizeFullName(fullName);
        var validatedPhone = ValidateAndNormalizePhone(phone);
        var validatedEmail = ValidateAndNormalizeEmail(email);

        FullName = validatedFullName;
        Phone = validatedPhone;
        Email = validatedEmail;
        UpdatedAt = changedAt;
    }

    /// <summary>
    /// Deactivates the customer profile. Historical order references remain intact,
    /// but the customer is excluded from active selection.
    /// </summary>
    public void Deactivate(UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);
        IsActive = false;
        UpdatedAt = changedAt;
    }

    /// <summary>
    /// Reactivates a previously deactivated customer profile.
    /// </summary>
    public void Activate(UtcTimestamp changedAt)
    {
        ArgumentNullException.ThrowIfNull(changedAt);
        IsActive = true;
        UpdatedAt = changedAt;
    }

    private static string ValidateAndNormalizeFullName(string fullName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        var trimmed = fullName.Trim();
        if (trimmed.Length > 120)
        {
            throw new ArgumentException("Full name cannot exceed 120 characters.", nameof(fullName));
        }

        return trimmed;
    }

    private static string ValidateAndNormalizePhone(string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        var trimmed = phone.Trim();
        if (trimmed.Length > 30)
        {
            throw new ArgumentException("Phone number cannot exceed 30 characters.", nameof(phone));
        }

        if (!PhoneRegex.IsMatch(trimmed) || trimmed.Count(char.IsDigit) < 7)
        {
            throw new ArgumentException("Phone number must contain at least 7 digits and valid phone characters.", nameof(phone));
        }

        return trimmed;
    }

    private static string? ValidateAndNormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim().ToLowerInvariant();
        if (trimmed.Length > 255)
        {
            throw new ArgumentException("Email address cannot exceed 255 characters.", nameof(email));
        }

        if (!EmailRegex.IsMatch(trimmed))
        {
            throw new ArgumentException($"Email '{email}' is not a valid email address.", nameof(email));
        }

        return trimmed;
    }
}
