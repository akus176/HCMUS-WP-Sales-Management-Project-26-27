namespace CoffeeShop.Api;

internal static class StaffMobileRelease
{
    public const string CounterScope = "Included in the Counter + Preparation staff mobile scope. Current Android acceptance is tracked in #206.";
    public const string CounterCustomerLookup = CounterScope + " Existing customer and saved-address lookup only; customer/address administration remains outside mobile scope.";
    public const string OutsideScope = "Outside the first staff mobile release.";
    public const string UsePreparationTickets = OutsideScope + " Staff Mobile uses the preparation-ticket endpoints.";
}
