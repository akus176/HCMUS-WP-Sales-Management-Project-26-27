namespace CoffeeShop.Application.Sales;

using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed record PosStaffOption(EntityId Id, string FullName);
public sealed record PosTableOption(EntityId Id, string TableNumber, int? Seats);
public sealed record PosOptions(IReadOnlyList<PosStaffOption> Staff, IReadOnlyList<PosTableOption> Tables);
public sealed record PosLineRequest(EntityId MenuItemId, EntityId SizeVariantId, int Quantity, IReadOnlyList<EntityId> AddonIds);
public sealed record CreatePosDraftCommand(EntityId OrderId, string Channel, EntityId? TableId,
    CustomerOrderSelectionRequest Customer, IReadOnlyList<PosLineRequest> Lines);
public sealed record PosAddonSnapshot(string Name, decimal UnitPrice);
public sealed record PosLineSnapshot(string ItemName, string SizeName, int Quantity, decimal UnitPrice,
    decimal Total, IReadOnlyList<PosAddonSnapshot> Addons);
public sealed record PosDiscountSummary(EntityId Id, string Type, decimal Value, string Status, decimal? Amount);
public sealed record PosOrderDetail(EntityId Id, string OrderNumber, string Channel, string Status,
    EntityId? CustomerId, string? CustomerName, EntityId? TableId, string? TableNumber,
    string? RecipientName, string? ContactNumber, string? AddressLine, EntityId? CustomerAddressId,
    decimal Subtotal, decimal DiscountTotal, decimal Total, IReadOnlyList<PosLineSnapshot> Lines,
    IReadOnlyList<PosDiscountSummary> Discounts)
{
    public static PosOrderDetail From(Order order, Delivery? delivery, string? customerName, string? tableNumber) => new(
        order.Id, order.OrderNumber, order.Channel, order.Status, order.CustomerId, customerName,
        order.TableId, tableNumber, delivery?.RecipientName, delivery?.ContactNumber, delivery?.AddressLine,
        delivery?.CustomerAddressId, order.Subtotal.Amount, order.DiscountTotal.Amount, order.Total.Amount,
        order.Lines.Select(line => new PosLineSnapshot(line.ItemNameSnapshot, line.SizeNameSnapshot,
            line.Quantity, line.UnitPriceSnapshot.Amount, line.LineTotal.Amount,
            line.Addons.Select(addon => new PosAddonSnapshot(addon.AddonNameSnapshot, addon.AddonPriceSnapshot.Amount)).ToArray())).ToArray(),
        order.Discounts.Select(discount => new PosDiscountSummary(discount.Id, discount.Type,
            discount.Value, discount.Status, discount.ComputedAmount?.Amount)).ToArray());
}
