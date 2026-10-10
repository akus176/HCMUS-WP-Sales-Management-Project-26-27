namespace CoffeeShop.Domain.Sales;

public static class DeliveryStatus
{
    public const string Unassigned = "unassigned";
    public const string Assigned = "assigned";
    public const string OutForDelivery = "out_for_delivery";
    public const string Delivered = "delivered";
    public const string Failed = "failed";

    public static bool IsKnown(string value) => value is Unassigned or Assigned or OutForDelivery or Delivered or Failed;
}
