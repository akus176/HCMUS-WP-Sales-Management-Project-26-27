namespace CoffeeShop.Domain.Sales;

public static class OrderStatus
{
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";
    public const string Preparing = "preparing";
    public const string Ready = "ready";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";

    public static bool IsKnown(string status) => status is Draft or Confirmed or Preparing or Ready or Completed or Cancelled;
}
