using System.ComponentModel;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.ViewModels;

// No XAML dependencies: the same presentation workflow runs in headless tests.
public sealed class CatalogWorkspace(IServiceScopeFactory scopes) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsBusy { get; private set; }
    public Error? Error { get; private set; }
    public bool IsError => Error is not null;
    public string Message { get; private set; } = "Load the catalog to begin.";
    public string Search { get; set; } = "";
    public bool IncludeInactive { get; set; }
    public IReadOnlyList<CategorySummary> Categories { get; private set; } = [];
    public IReadOnlyList<MenuItemSummary> Items { get; private set; } = [];
    public IReadOnlyList<AddonSummary> Addons { get; private set; } = [];
    public IReadOnlyList<RecipeIngredientOption> Ingredients { get; private set; } = [];
    public MenuItemDetail? SelectedItem { get; private set; }
    public bool IsEmpty => !IsBusy && !IsError && Items.Count == 0;
    public WorkflowFeedback Feedback => WorkflowFeedback.Resolve(IsBusy, IsEmpty, Error, Message);

    public Task LoadAsync() => RunAsync(async () =>
    {
        await ReloadAsync();
        if (!IsError)
        {
            Message = Items.Count == 200
            ? "Showing the first 200 matches. Narrow the search to find more drinks."
            : "Catalog loaded. Select a drink or create a new one.";
        }
    });

    public Task SelectAsync(EntityId id) => RunAsync(async () =>
    {
        SelectedItem = null;
        using var scope = scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<CatalogQueryHandler>().GetMenuItemDetailAsync(id);
        if (Accept(result))
        {
            SelectedItem = result.Value;
            Message = $"Editing {SelectedItem.Name}. Changes are saved per section.";
        }
    });

    public async Task<bool> SaveAsync(
        Func<CatalogCommandHandler, RecipeCommandHandler, Task<Result>> action,
        string successMessage)
    {
        var saved = false;
        await RunAsync(async () =>
        {
            // A fresh context for each action also discards failed tracked mutations.
            using (var scope = scopes.CreateScope())
            {
                var provider = scope.ServiceProvider;
                saved = Accept(await action(provider.GetRequiredService<CatalogCommandHandler>(),
                    provider.GetRequiredService<RecipeCommandHandler>()));
            }

            if (saved)
            {
                Message = successMessage;
                try
                {
                    await ReloadAsync();
                }
                catch (Exception)
                {
                    // The write succeeded: do not invite a duplicate create on refresh failure.
                    Message = $"{successMessage} Refresh failed. Use Reload before continuing; do not submit again.";
                    Error = Error.Unexpected("catalog.database_error", Message);
                }
            }
        });
        return saved;
    }

    public void ShowValidation(string message)
    {
        Error = Error.Validation("catalog.ui_validation", message, [new("input", message)]);
        Message = message;
        Notify();
    }

    private async Task ReloadAsync()
    {
        using var scope = scopes.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<CatalogQueryHandler>();
        var categories = await queries.SearchCategoriesAsync(new CategorySearchCriteria(Limit: 200));
        var items = await queries.SearchMenuItemsAsync(new MenuItemSearchCriteria(
            Search: Search, IsActive: IncludeInactive ? null : true, Limit: 200));
        if (!Accept(categories) || !Accept(items))
        {
            return;
        }

        var addons = await queries.ListAddonsAsync();
        var ingredients = await queries.ListActiveRecipeIngredientsAsync();
        Categories = categories.Value;
        Items = items.Value;
        Addons = addons.Value;
        Ingredients = ingredients.Value;
        if (SelectedItem is not null)
        {
            var detail = await queries.GetMenuItemDetailAsync(SelectedItem.Id);
            SelectedItem = detail.IsSuccess ? detail.Value : null;
        }
    }

    private bool Accept(Result result)
    {
        if (result.IsSuccess)
        {
            return true;
        }

        var error = result.Error!;
        Error = error;
        Message = $"{error.Type}: {error.Description}";
        if (error.Details.Count > 0)
        {
            Message += "\n" + string.Join("\n", error.Details.Select(detail => $"{detail.Target}: {detail.Message}"));
        }

        return false;
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        Message = "Working...";
        Notify();
        try
        {
            await action();
        }
        catch (Exception)
        {
            Message = "The operation could not complete. Check the local database connection, then reload and verify the saved state before retrying.";
            Error = Error.Unexpected("catalog.database_error", Message);
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
