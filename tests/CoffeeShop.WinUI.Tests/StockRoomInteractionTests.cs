using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.ViewModels;

namespace CoffeeShop.WinUI.Tests;

public sealed class StockRoomInteractionTests
{
    [Fact]
    public void PrimaryTabs_DoNotDuplicateTheFilterOrAdjustmentAction()
    {
        Assert.Equal(["Inventory", "Receipts", "Suppliers", "History"], StockRoomInteraction.Tabs.Select(tab => tab.Label));
        Assert.Equal(4, StockRoomInteraction.Tabs.Select(tab => tab.Tag).Distinct().Count());
    }

    [Theory]
    [InlineData("ingredients", "ingredients", false, false)]
    [InlineData("lowstock", "ingredients", true, false)]
    [InlineData("adjustments", "ingredients", false, true)]
    [InlineData("receipts", "receipts", false, false)]
    [InlineData("suppliers", "suppliers", false, false)]
    [InlineData("movements", "movements", false, false)]
    public void LegacyFunctions_EnterTheCorrespondingTabFilterOrDrawer(string route, string tab, bool low, bool adjust)
    {
        Assert.Equal(new StockRoomEntry(tab, low, adjust), StockRoomInteraction.Resolve(route));
    }

    [Fact]
    public void Drafts_StayWithTheirIngredientAndCanBeCancelledIndependently()
    {
        var drafts = new StockAdjustmentDrafts();
        var beans = EntityId.New();
        var milk = EntityId.New();
        drafts.Remember(beans, "-10", "Calibration");
        Assert.Equal(new StockAdjustmentDraft(), drafts.For(milk));
        drafts.Remember(milk, "+5", "Count correction");
        Assert.Equal(new StockAdjustmentDraft("-10", "Calibration"), drafts.For(beans));
        drafts.Clear(beans);
        Assert.Equal(new StockAdjustmentDraft(), drafts.For(beans));
        Assert.Equal("Count correction", drafts.For(milk).Reason);
    }

    [Theory]
    [InlineData("", true, false)]
    [InlineData("0", true, false)]
    [InlineData("-101", true, false)]
    [InlineData("1.0001", true, false)]
    [InlineData("-79228162514264337593543950335", true, false)]
    [InlineData("79228162514264337593543950335", true, false)]
    [InlineData("-5", false, false)]
    [InlineData("-5", true, true)]
    public void Submission_RequiresAValidResultAndOperator(string quantity, bool actor, bool expected)
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var ingredient = new Ingredient(EntityId.New(), "Beans", "g", now, now, availableQuantity: 100);
        var preview = StockAdjustmentPreview.Create(ingredient, quantity, actor);
        Assert.Equal(expected, preview.CanSubmit);
        Assert.False(string.IsNullOrWhiteSpace(preview.Hint));
        if (expected) Assert.Equal(95m, preview.Balance);
    }
}
