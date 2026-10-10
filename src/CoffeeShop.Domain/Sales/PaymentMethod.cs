namespace CoffeeShop.Domain.Sales;

public static class PaymentMethod
{
    public const string Cash = "cash";
    public const string Card = "card";
    public const string EWallet = "e_wallet";

    public static bool IsKnown(string method) => method is Cash or Card or EWallet;

    public static bool RequiresReference(string method) => method is Card or EWallet;
}
