namespace CoffeeShop.Domain.Customers;

using CoffeeShop.Domain.Common;

public class CustomerAddress
{
    // Parameterless constructor for EF Core
    protected CustomerAddress()
    {
    }

    public CustomerAddress(
        EntityId id,
        EntityId customerId,
        string addressLine,
        string? label = null,
        bool isDefault = false,
        bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(customerId);

        Id = id;
        CustomerId = customerId;
        AddressLine = ValidateAndNormalizeAddressLine(addressLine);
        Label = ValidateAndNormalizeLabel(label);
        IsActive = isActive;
        if (isDefault && !isActive)
        {
            throw new InvalidOperationException("Cannot set a deactivated address as default.");
        }

        IsDefault = isDefault;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId CustomerId { get; protected set; } = null!;

    public Customer? Customer { get; protected set; }

    public string? Label { get; protected set; }

    public string AddressLine { get; protected set; } = string.Empty;

    public bool IsDefault { get; protected set; }

    public bool IsActive { get; protected set; } = true;

    public void Update(string addressLine, string? label, bool isDefault)
    {
        var validatedAddressLine = ValidateAndNormalizeAddressLine(addressLine);
        var validatedLabel = ValidateAndNormalizeLabel(label);

        if (isDefault && !IsActive)
        {
            throw new InvalidOperationException("Cannot set a deactivated address as default.");
        }

        AddressLine = validatedAddressLine;
        Label = validatedLabel;
        IsDefault = isDefault;
    }

    public void Deactivate()
    {
        IsActive = false;
        IsDefault = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void SetDefault(bool isDefault)
    {
        if (isDefault && !IsActive)
        {
            throw new InvalidOperationException("Cannot set a deactivated address as default.");
        }

        IsDefault = isDefault;
    }

    private static string ValidateAndNormalizeAddressLine(string addressLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressLine);
        var trimmed = addressLine.Trim();
        if (trimmed.Length > 500)
        {
            throw new ArgumentException("Address line cannot exceed 500 characters.", nameof(addressLine));
        }

        return trimmed;
    }

    private static string? ValidateAndNormalizeLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var trimmed = label.Trim();
        if (trimmed.Length > 40)
        {
            throw new ArgumentException("Address label cannot exceed 40 characters.", nameof(label));
        }

        return trimmed;
    }
}

