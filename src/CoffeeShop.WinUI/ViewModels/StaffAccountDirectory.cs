using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.ViewModels;

public static class StaffAccountDirectory
{
    public static StaffAccountResponse[] IncludeCreated(
        StaffAccountResponse created, StaffAccountResponse[] firstPage) =>
        [created, .. firstPage.Where(account => account.Id != created.Id)];

    public static StaffAccountResponse[] IncludeUpdated(
        StaffAccountResponse updated, StaffAccountResponse[] firstPage) =>
        [updated, .. firstPage.Where(account => account.Id != updated.Id)];
}
