namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;

/// <summary>
/// Represents delivery selection details for an order, supporting both reusable customer addresses
/// and order-specific one-off addresses.
/// </summary>
public sealed record DeliverySelection
{
    public EntityId? CustomerAddressId { get; init; }
    public string? AddressLine { get; init; }
    public string RecipientName { get; init; } = string.Empty;
    public string ContactNumber { get; init; } = string.Empty;

    public bool IsReusable => CustomerAddressId is not null;
    public bool IsOrderSpecific => CustomerAddressId is null;

    public static DeliverySelection FromCustomerAddress(
        EntityId customerAddressId,
        string recipientName,
        string contactNumber) =>
        new()
        {
            CustomerAddressId = customerAddressId,
            RecipientName = recipientName,
            ContactNumber = contactNumber,
        };

    public static DeliverySelection OrderSpecific(
        string addressLine,
        string recipientName,
        string contactNumber) =>
        new()
        {
            AddressLine = addressLine,
            RecipientName = recipientName,
            ContactNumber = contactNumber,
        };
}
