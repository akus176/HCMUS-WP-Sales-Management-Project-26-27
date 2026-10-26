using System.ComponentModel;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.ViewModels;

public sealed class InventoryAuditWorkspace(IServiceScopeFactory scopes, PosOperatorSession session) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<Ingredient> Ingredients { get; private set; } = [];
    public IReadOnlyList<PosStaffOption> Staff { get; private set; } = [];
    public IReadOnlyList<StockMovement> Movements { get; private set; } = [];
    public EntityId? SelectedIngredientId { get; private set; }
    public Ingredient? SelectedIngredient => Ingredients.SingleOrDefault(ingredient => ingredient.Id == SelectedIngredientId);
    public PosOperatorSession Session => session;
    public bool IsBusy { get; private set; }
    public bool RequiresReload { get; private set; }
    public Error? Error { get; private set; }
    public string Message { get; private set; } = "Choose an ingredient to inspect its balance and latest 100 movements.";
    public bool CanAdjust => !IsBusy && !RequiresReload && session.IsAuthenticated && SelectedIngredient is { IsActive: true };
    public WorkflowFeedback Feedback => WorkflowFeedback.Resolve(IsBusy, Ingredients.Count == 0, Error, Message);

    public Task LoadAsync() => RunAsync(async provider =>
    {
        Staff = (await provider.GetRequiredService<PosOrderHandler>().GetOptionsAsync()).Staff;
        if (session.Operator is { } actor && Staff.All(staff => staff.Id != actor.Id)) session.Operator = null;
        await ReadAsync(provider);
        RequiresReload = false;
        Message = "Balances and latest 100 movements loaded. Select an operator before recording an adjustment.";
    });

    public Task SelectAsync(EntityId ingredientId)
    {
        SelectedIngredientId = ingredientId;
        return LoadAsync();
    }

    public Task AdjustAsync(decimal delta, string reason) => RunAsync(async provider =>
    {
        if (RequiresReload) return;
        if (SelectedIngredientId is null || session.Id is null || Staff.All(staff => staff.Id != session.Id))
        {
            Error = Error.Validation("inventory.selection_required", "Select an ingredient and an active operator.");
            return;
        }
        var result = await provider.GetRequiredService<InventoryCommandHandler>().RecordStockAdjustmentAsync(
            new(SelectedIngredientId, delta, reason, session.Id));
        if (result.IsFailure) { Error = result.Error; return; }
        RequiresReload = true;
        await ReadAsync(provider);
        RequiresReload = false;
        Message = "Adjustment recorded. The balance and immutable movement history have been refreshed.";
    });

    public void ShowValidation(string message)
    {
        Error = Error.Validation("inventory.invalid_input", message);
        PropertyChanged?.Invoke(this, new(null));
    }

    private async Task ReadAsync(IServiceProvider provider)
    {
        Ingredients = await provider.GetRequiredService<IInventoryRepository>().ListIngredientsAsync();
        if (SelectedIngredientId is null || Ingredients.All(ingredient => ingredient.Id != SelectedIngredientId))
            SelectedIngredientId = Ingredients.FirstOrDefault()?.Id;
        Movements = SelectedIngredientId is null ? []
            : await provider.GetRequiredService<IStockMovementReader>().ListRecentAsync(SelectedIngredientId);
    }

    private async Task RunAsync(Func<IServiceProvider, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Error = null; PropertyChanged?.Invoke(this, new(null));
        try { using var scope = scopes.CreateScope(); await action(scope.ServiceProvider); }
        catch (Exception)
        {
            RequiresReload = true;
            Error = Error.Unexpected("inventory.database_error", "Could not finish the operation. Reload and inspect history before retrying because a write may have succeeded.");
        }
        finally { IsBusy = false; PropertyChanged?.Invoke(this, new(null)); }
    }
}
