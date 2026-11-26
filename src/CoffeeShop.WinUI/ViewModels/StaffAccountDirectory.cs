using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.ViewModels;

public static class StaffAccountDirectory
{
    public static StaffAccountResponse[] IncludeCreated(
        StaffAccountResponse created, StaffAccountResponse[] firstPage) =>
        [created, .. firstPage.Where(account => account.Id != created.Id)];
}
