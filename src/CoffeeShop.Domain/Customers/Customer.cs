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

    /// <summary>
    /// Adds a new delivery address to this customer.
    /// Explicit default-address rules:
    /// - If isDefault is true, this address becomes default and any previous default is unset.
    /// - If isDefault is false and there is currently no active default address, this address becomes default.
    /// - Address ownership belongs exclusively to this customer.
    /// </summary>
    public CustomerAddress AddAddress(
        EntityId addressId,
        string addressLine,
        string? label = null,
        bool isDefault = false,
        UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(addressId);

        if (_addresses.Any(a => a.Id == addressId))
        {
            throw new InvalidOperationException($"An address with ID '{addressId}' already exists for this customer.");
        }

        var shouldBeDefault = isDefault || !_addresses.Any(a => a.IsActive && a.IsDefault);

        if (shouldBeDefault)
        {
            foreach (var existing in _addresses)
            {
                existing.SetDefault(false);
            }
        }

        var address = new CustomerAddress(addressId, Id, addressLine, label, isDefault: shouldBeDefault, isActive: true);
        _addresses.Add(address);

        if (changedAt is not null)
        {
            UpdatedAt = changedAt;
        }

        return address;
    }

    /// <summary>
    /// Updates an address belonging to this customer.
    /// Throws InvalidOperationException if the address does not belong to this customer or if attempting to set a deactivated address as default.
    /// </summary>
    public CustomerAddress UpdateAddress(
        EntityId addressId,
        string addressLine,
        string? label,
        bool isDefault,
        UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(addressId);

        var address = _addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new InvalidOperationException($"Address '{addressId}' does not belong to customer '{Id}'.");

        if (isDefault)
        {
            if (!address.IsActive)
            {
                throw new InvalidOperationException("Cannot set a deactivated address as default.");
            }

            foreach (var other in _addresses.Where(a => a.Id != addressId))
            {
                other.SetDefault(false);
            }
        }

        address.Update(addressLine, label, isDefault);

        if (changedAt is not null)
        {
            UpdatedAt = changedAt;
        }

        return address;
    }

    /// <summary>
    /// Selects an active address as the default delivery address for this customer.
    /// Clears any previous default address.
    /// Throws InvalidOperationException if the address does not belong to this customer or is deactivated.
    /// </summary>
    public CustomerAddress SetDefaultAddress(EntityId addressId, UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(addressId);

        var address = _addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new InvalidOperationException($"Address '{addressId}' does not belong to customer '{Id}'.");

        if (!address.IsActive)
        {
            throw new InvalidOperationException("Cannot set a deactivated address as default.");
        }

        foreach (var other in _addresses.Where(a => a.Id != addressId))
        {
            other.SetDefault(false);
        }

        address.SetDefault(true);

        if (changedAt is not null)
        {
            UpdatedAt = changedAt;
        }

        return address;
    }

    /// <summary>
    /// Deactivates an address. Deactivating clears its default status.
    /// Throws InvalidOperationException if the address does not belong to this customer.
    /// </summary>
    public CustomerAddress DeactivateAddress(EntityId addressId, UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(addressId);

        var address = _addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new InvalidOperationException($"Address '{addressId}' does not belong to customer '{Id}'.");

        address.Deactivate();

        if (changedAt is not null)
        {
            UpdatedAt = changedAt;
        }

        return address;
    }

    /// <summary>
    /// Reactivates an address. If no active address is currently default, this address becomes default.
    /// Throws InvalidOperationException if the address does not belong to this customer.
    /// </summary>
    public CustomerAddress ActivateAddress(EntityId addressId, UtcTimestamp? changedAt = null)
    {
        ArgumentNullException.ThrowIfNull(addressId);

        var address = _addresses.FirstOrDefault(a => a.Id == addressId)
            ?? throw new InvalidOperationException($"Address '{addressId}' does not belong to customer '{Id}'.");

        address.Activate();

        if (!_addresses.Any(a => a.IsActive && a.IsDefault))
        {
            address.SetDefault(true);
        }

        if (changedAt is not null)
        {
            UpdatedAt = changedAt;
        }

        return address;
    }

    public CustomerAddress? GetDefaultAddress() =>
        _addresses.FirstOrDefault(a => a.IsActive && a.IsDefault);

    public CustomerAddress? GetAddress(EntityId addressId) =>
        _addresses.FirstOrDefault(a => a.Id == addressId);

    public IReadOnlyList<CustomerAddress> GetActiveAddresses() =>
        _addresses.Where(a => a.IsActive).ToList().AsReadOnly();

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
