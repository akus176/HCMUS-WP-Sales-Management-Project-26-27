using CoffeeShop.Contracts;
using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class StaffAccountDirectoryTests
{
    [Fact]
    public void IncludeCreated_PinsNewAccountBeyondFirstPageWithoutDroppingExistingRows()
    {
        var firstPage = Enumerable.Range(0, 100).Select(index => Account($"a{index:000}")).ToArray();
        var created = Account("z.created");

        var display = StaffAccountDirectory.IncludeCreated(created, firstPage);

        Assert.Equal(created.Id, display[0].Id);
        Assert.Equal(101, display.Length);
        Assert.All(firstPage, account => Assert.Contains(display, item => item.Id == account.Id));
    }

    [Fact]
    public void IncludeCreated_DoesNotDuplicateAccountAlreadyInDirectory()
    {
        var created = Account("new.cashier");

        var display = StaffAccountDirectory.IncludeCreated(created, [Account("earlier"), created]);

        Assert.Equal(created.Id, display[0].Id);
        Assert.Equal(2, display.Length);
        Assert.Single(display, account => account.Id == created.Id);
    }

    private static StaffAccountResponse Account(string username) => new(
        Guid.NewGuid(), username, username, $"{username}@example.test", null, true,
        ["Cashier"], DateTimeOffset.UtcNow, null);
}
