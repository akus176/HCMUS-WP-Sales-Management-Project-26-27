namespace CoffeeShop.Domain.Sales;

public static class PaymentStatus
{
    public const string Pending = "pending";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";

    public static bool IsKnown(string status) => status is Pending or Succeeded or Failed;

    public static bool CountsTowardPaidBalance(string status) => status == Succeeded;
}
