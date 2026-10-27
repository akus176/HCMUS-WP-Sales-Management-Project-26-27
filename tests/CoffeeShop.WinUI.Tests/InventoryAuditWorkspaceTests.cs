using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class InventoryAuditWorkspaceTests
{
    [Fact]
    public async Task FailedRead_PreservesSelectionAndBlocksAdjustmentsUntilReload()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var model = new InventoryAuditWorkspace(provider.GetRequiredService<IServiceScopeFactory>(), new());
        var ingredient = EntityId.New();
        await model.SelectAsync(ingredient);
        Assert.Equal(ingredient, model.SelectedIngredientId);
        Assert.True(model.RequiresReload);
        Assert.False(model.CanAdjust);
        Assert.False(model.IsBusy);
        Assert.Equal(WorkflowFeedbackKind.DatabaseError, model.Feedback.Kind);
        Assert.Contains("write may have succeeded", model.Error!.Description);
    }

    [Fact]
    public void NewWorkspace_RequiresAnIngredientAndOperatorBeforeAdjustment()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var model = new InventoryAuditWorkspace(provider.GetRequiredService<IServiceScopeFactory>(), new());
        Assert.False(model.CanAdjust);
        Assert.Empty(model.Ingredients);
        Assert.Empty(model.Movements);
        model.ShowValidation("Enter a quantity without grouping separators.");
        Assert.Equal(WorkflowFeedbackKind.Validation, model.Feedback.Kind);
        Assert.Contains("quantity", model.Error!.Description);
        Assert.False(model.RequiresReload);
    }
}
