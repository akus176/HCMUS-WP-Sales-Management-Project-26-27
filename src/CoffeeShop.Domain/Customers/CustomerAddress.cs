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
        bool isDefault = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(customerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressLine);

        Id = id;
        CustomerId = customerId;
        AddressLine = addressLine.Trim();
        Label = label?.Trim();
        IsDefault = isDefault;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId CustomerId { get; protected set; } = null!;

    public Customer? Customer { get; protected set; }

    public string? Label { get; protected set; }

    public string AddressLine { get; protected set; } = string.Empty;

    public bool IsDefault { get; protected set; }
}
