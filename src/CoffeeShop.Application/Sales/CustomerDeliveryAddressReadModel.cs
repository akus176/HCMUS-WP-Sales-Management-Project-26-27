namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;

/// <summary>
/// Read contract representing a selectable customer delivery address for the sales module.
/// Decouples sales and POS delivery selection from the customer aggregate implementation.
/// </summary>
public sealed record CustomerDeliveryAddressReadModel(
    EntityId AddressId,
    EntityId CustomerId,
    string AddressLine,
    string? Label,
    bool IsDefault,
    bool IsActive);
