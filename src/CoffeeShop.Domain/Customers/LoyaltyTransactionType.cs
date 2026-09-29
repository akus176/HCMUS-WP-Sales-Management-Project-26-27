namespace CoffeeShop.Domain.Customers;

public static class LoyaltyTransactionType
{
    public const string Earn = "earn";
    public const string Redeem = "redeem";
    public const string ReverseEarn = "reverse_earn";
    public const string ReverseRedeem = "reverse_redeem";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        Earn,
        Redeem,
        ReverseEarn,
        ReverseRedeem
    };

    public static bool IsValid(string type) => All.Contains(type);
}
