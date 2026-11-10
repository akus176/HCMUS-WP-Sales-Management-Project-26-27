using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Contracts;
using CoffeeShop.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Services;

public interface ICatalogWorkspaceSource
{
    Task<Result<IReadOnlyList<CategorySummary>>> SearchCategoriesAsync(CategorySearchCriteria criteria);
    Task<Result<IReadOnlyList<MenuItemSummary>>> SearchMenuItemsAsync(MenuItemSearchCriteria criteria);
    Task<Result<IReadOnlyList<AddonSummary>>> ListAddonsAsync();
    Task<Result<IReadOnlyList<RecipeIngredientOption>>> ListActiveRecipeIngredientsAsync();
    Task<Result<MenuItemDetail>> GetMenuItemDetailAsync(EntityId id);
    Task<Result> CreateCategoryAsync(CreateCategoryCommand command);
    Task<Result> UpdateCategoryAsync(UpdateCategoryCommand command);
    Task<Result> DeactivateCategoryAsync(DeactivateCategoryCommand command);
    Task<Result> CreateMenuItemAsync(CreateMenuItemCommand command);
    Task<Result> UpdateMenuItemAsync(UpdateMenuItemCommand command);
    Task<Result> DeactivateMenuItemAsync(DeactivateMenuItemCommand command);
    Task<Result> CreateSizeVariantAsync(CreateSizeVariantCommand command);
    Task<Result> UpdateSizeVariantAsync(UpdateSizeVariantCommand command);
    Task<Result> DeactivateSizeVariantAsync(DeactivateSizeVariantCommand command);
    Task<Result> CreateAddonAsync(CreateAddonCommand command);
    Task<Result> UpdateAddonAsync(UpdateAddonCommand command);
    Task<Result> DeactivateAddonAsync(DeactivateAddonCommand command);
    Task<Result> SetMenuItemAddonsAsync(SetMenuItemAddonsCommand command);
    Task<Result> ReplaceRecipeAsync(ReplaceRecipeCommand command);
}

public sealed class DirectCatalogWorkspaceSource(IServiceScopeFactory scopes) : ICatalogWorkspaceSource
{
    public Task<Result<IReadOnlyList<CategorySummary>>> SearchCategoriesAsync(CategorySearchCriteria criteria) =>
        QueryAsync(async service => Convert(await service.GetRequiredService<CatalogQueryHandler>().SearchCategoriesAsync(criteria), rows => (IReadOnlyList<CategorySummary>)rows));
    public Task<Result<IReadOnlyList<MenuItemSummary>>> SearchMenuItemsAsync(MenuItemSearchCriteria criteria) =>
        QueryAsync(async service => Convert(await service.GetRequiredService<CatalogQueryHandler>().SearchMenuItemsAsync(criteria), rows => (IReadOnlyList<MenuItemSummary>)rows));
    public Task<Result<IReadOnlyList<AddonSummary>>> ListAddonsAsync() =>
        QueryAsync(async service => Convert(await service.GetRequiredService<CatalogQueryHandler>().ListAddonsAsync(), rows => (IReadOnlyList<AddonSummary>)rows));
    public Task<Result<IReadOnlyList<RecipeIngredientOption>>> ListActiveRecipeIngredientsAsync() =>
        QueryAsync(async service => Convert(await service.GetRequiredService<CatalogQueryHandler>().ListActiveRecipeIngredientsAsync(), rows => (IReadOnlyList<RecipeIngredientOption>)rows));
    public Task<Result<MenuItemDetail>> GetMenuItemDetailAsync(EntityId id) =>
        QueryAsync(service => service.GetRequiredService<CatalogQueryHandler>().GetMenuItemDetailAsync(id));
    public Task<Result> CreateCategoryAsync(CreateCategoryCommand command) => CatalogIdAsync(handler => handler.CreateCategoryAsync(command));
    public Task<Result> UpdateCategoryAsync(UpdateCategoryCommand command) => CatalogAsync(handler => handler.UpdateCategoryAsync(command));
    public Task<Result> DeactivateCategoryAsync(DeactivateCategoryCommand command) => CatalogAsync(handler => handler.DeactivateCategoryAsync(command));
    public Task<Result> CreateMenuItemAsync(CreateMenuItemCommand command) => CatalogIdAsync(handler => handler.CreateMenuItemAsync(command));
    public Task<Result> UpdateMenuItemAsync(UpdateMenuItemCommand command) => CatalogAsync(handler => handler.UpdateMenuItemAsync(command));
    public Task<Result> DeactivateMenuItemAsync(DeactivateMenuItemCommand command) => CatalogAsync(handler => handler.DeactivateMenuItemAsync(command));
    public Task<Result> CreateSizeVariantAsync(CreateSizeVariantCommand command) => CatalogIdAsync(handler => handler.CreateSizeVariantAsync(command));
    public Task<Result> UpdateSizeVariantAsync(UpdateSizeVariantCommand command) => CatalogAsync(handler => handler.UpdateSizeVariantAsync(command));
    public Task<Result> DeactivateSizeVariantAsync(DeactivateSizeVariantCommand command) => CatalogAsync(handler => handler.DeactivateSizeVariantAsync(command));
    public Task<Result> CreateAddonAsync(CreateAddonCommand command) => CatalogIdAsync(handler => handler.CreateAddonAsync(command));
    public Task<Result> UpdateAddonAsync(UpdateAddonCommand command) => CatalogAsync(handler => handler.UpdateAddonAsync(command));
    public Task<Result> DeactivateAddonAsync(DeactivateAddonCommand command) => CatalogAsync(handler => handler.DeactivateAddonAsync(command));
    public Task<Result> SetMenuItemAddonsAsync(SetMenuItemAddonsCommand command) => CatalogAsync(handler => handler.SetMenuItemAddonsAsync(command));
    public async Task<Result> ReplaceRecipeAsync(ReplaceRecipeCommand command)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RecipeCommandHandler>().ReplaceRecipeAsync(command);
    }

    private async Task<Result<T>> QueryAsync<T>(Func<IServiceProvider, Task<Result<T>>> operation) where T : notnull
    {
        using var scope = scopes.CreateScope();
        return await operation(scope.ServiceProvider);
    }

    private static Result<TOut> Convert<TIn, TOut>(Result<TIn> result, Func<TIn, TOut> convert) where TIn : notnull where TOut : notnull =>
        result.IsSuccess ? Result<TOut>.Success(convert(result.Value)) : Result<TOut>.Failure(result.Error!);

    private async Task<Result> CatalogAsync(Func<CatalogCommandHandler, Task<Result>> operation)
    {
        using var scope = scopes.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<CatalogCommandHandler>());
    }

    private async Task<Result> CatalogIdAsync(Func<CatalogCommandHandler, Task<Result<EntityId>>> operation)
    {
        using var scope = scopes.CreateScope();
        var result = await operation(scope.ServiceProvider.GetRequiredService<CatalogCommandHandler>());
        return result.IsSuccess ? Result.Success() : Result.Failure(result.Error!);
    }
}

public sealed class ApiCatalogWorkspaceSource(AuthenticatedApiClient client) : ICatalogWorkspaceSource
{
    private const int Limit = 200;

    public async Task<Result<IReadOnlyList<CategorySummary>>> SearchCategoriesAsync(CategorySearchCriteria criteria) =>
        await RunAsync(async () => (IReadOnlyList<CategorySummary>)(await client.GetAsync<CategoryResponse[]>($"categories?{Query(criteria.Search, criteria.IsActive, criteria.Limit)}"))
            .Select(item => new CategorySummary(new(item.Id), item.Name, item.DisplayOrder, item.IsActive)).ToArray());

    public async Task<Result<IReadOnlyList<MenuItemSummary>>> SearchMenuItemsAsync(MenuItemSearchCriteria criteria) =>
        await RunAsync(async () => (IReadOnlyList<MenuItemSummary>)(await client.GetAsync<MenuItemResponse[]>($"menu-items?{Query(criteria.Search, criteria.IsActive, criteria.Limit, criteria.CategoryId?.Value)}"))
            .Select(ToSummary).ToArray());

    public async Task<Result<IReadOnlyList<AddonSummary>>> ListAddonsAsync() =>
        await RunAsync(async () => (IReadOnlyList<AddonSummary>)(await client.GetAsync<AddonResponse[]>("addons"))
            .Select(row => new AddonSummary(new(row.Id), row.Name, row.Price, row.IsActive)).ToArray());

    public async Task<Result<IReadOnlyList<RecipeIngredientOption>>> ListActiveRecipeIngredientsAsync() =>
        await RunAsync(async () => (IReadOnlyList<RecipeIngredientOption>)(await client.GetAsync<PageResponse<IngredientBalanceResponse>>("ingredients?isActive=true&page=1&pageSize=200"))
            .Items.Select(row => new RecipeIngredientOption(new(row.IngredientId), row.Name, row.BaseUnit)).ToArray());

    public Task<Result<MenuItemDetail>> GetMenuItemDetailAsync(EntityId id) => RunAsync(async () => ToDetail(await client.GetAsync<MenuItemResponse>($"menu-items/{id.Value}")));

    public async Task<Result> CreateCategoryAsync(CreateCategoryCommand command) => await RunCommandAsync(async () =>
        _ = await client.PostAsync<CreateCategoryRequest, CategoryResponse>("categories", new(command.Name, command.DisplayOrder)));
    public Task<Result> UpdateCategoryAsync(UpdateCategoryCommand command) => RunCommandAsync(() => client.PutAsync<CreateCategoryRequest, object>($"categories/{command.CategoryId.Value}", new(command.Name)));
    public Task<Result> DeactivateCategoryAsync(DeactivateCategoryCommand command) => RunCommandAsync(() => client.PostAsync<object, object>($"categories/{command.CategoryId.Value}/deactivate", new { }));
    public async Task<Result> CreateMenuItemAsync(CreateMenuItemCommand command) => await RunCommandAsync(async () =>
        _ = await client.PostAsync<MenuItemRequest, MenuItemResponse>("menu-items", new(command.CategoryId.Value, command.Name, command.Description,
            command.InitialVariants.Select(variant => new SizeVariantRequest(variant.SizeName, variant.Price)).ToArray())));
    public Task<Result> UpdateMenuItemAsync(UpdateMenuItemCommand command) => RunCommandAsync(() => client.PutAsync<MenuItemRequest, object>($"menu-items/{command.MenuItemId.Value}", new(command.CategoryId.Value, command.Name, command.Description)));
    public Task<Result> DeactivateMenuItemAsync(DeactivateMenuItemCommand command) => RunCommandAsync(() => client.PostAsync<object, object>($"menu-items/{command.MenuItemId.Value}/deactivate", new { }));
    public Task<Result> CreateSizeVariantAsync(CreateSizeVariantCommand command) => RunCommandAsync(() => client.PostAsync<SizeVariantRequest, object>($"menu-items/{command.MenuItemId.Value}/variants", new(command.SizeName, command.Price)));
    public Task<Result> UpdateSizeVariantAsync(UpdateSizeVariantCommand command) => RunCommandAsync(() => client.PutAsync<SizeVariantRequest, object>($"menu-items/{command.MenuItemId.Value}/variants/{command.SizeVariantId.Value}", new(command.SizeName, command.Price)));
    public Task<Result> DeactivateSizeVariantAsync(DeactivateSizeVariantCommand command) => RunCommandAsync(() => client.PostAsync<object, object>($"size-variants/{command.SizeVariantId.Value}/deactivate", new { }));
    public Task<Result> CreateAddonAsync(CreateAddonCommand command) => RunCommandAsync(() => client.PostAsync<AddonRequest, object>("addons", new(command.Name, command.Price)));
    public Task<Result> UpdateAddonAsync(UpdateAddonCommand command) => RunCommandAsync(() => client.PutAsync<AddonRequest, object>($"addons/{command.AddonId.Value}", new(command.Name, command.Price)));
    public Task<Result> DeactivateAddonAsync(DeactivateAddonCommand command) => RunCommandAsync(() => client.PostAsync<object, object>($"addons/{command.AddonId.Value}/deactivate", new { }));
    public Task<Result> SetMenuItemAddonsAsync(SetMenuItemAddonsCommand command) => RunCommandAsync(() => client.PutAsync<SetCompatibleAddonsRequest, object>($"menu-items/{command.MenuItemId.Value}/addons", new(command.AddonIds.Select(id => id.Value).ToArray())));
    public Task<Result> ReplaceRecipeAsync(ReplaceRecipeCommand command) => RunCommandAsync(() => client.PutAsync<ReplaceRecipeRequest, object>($"recipes/{command.SizeVariantId.Value}", new(command.Lines.Select(line => new RecipeLineRequest(line.IngredientId.Value, line.Quantity, line.Unit)).ToArray())));

    private static MenuItemSummary ToSummary(MenuItemResponse item) => new(new(item.Id), new(item.CategoryId), item.CategoryName,
        item.Name, item.Description, item.IsActive, item.SizeVariants.Select(size => new SizeVariantSummary(new(size.Id), size.SizeName, size.Price, size.IsActive)).ToArray(),
        item.CompatibleAddons.Select(addon => new AddonSummary(new(addon.Id), addon.Name, addon.Price, addon.IsActive)).ToArray());

    private static MenuItemDetail ToDetail(MenuItemResponse item) => new(new(item.Id), new(item.CategoryId), item.CategoryName,
        item.Name, item.Description, item.IsActive, item.SizeVariants.Select(size => new SizeVariantDetail(new(size.Id), size.SizeName,
            size.Price, size.IsActive, (size.Recipe ?? []).Select(line => new RecipeLineDetail(new(line.Id), new(line.IngredientId),
                line.IngredientName, line.Quantity, line.Unit)).ToArray())).ToArray(),
        item.CompatibleAddons.Select(addon => new AddonSummary(new(addon.Id), addon.Name, addon.Price, addon.IsActive)).ToArray());

    private static string Query(string? search, bool? isActive, int limit, Guid? categoryId = null) =>
        $"search={Uri.EscapeDataString(search ?? string.Empty)}&limit={Math.Clamp(limit, 1, Limit)}" +
        (isActive is { } active ? $"&isActive={active.ToString().ToLowerInvariant()}" : string.Empty) +
        (categoryId is null ? string.Empty : $"&categoryId={categoryId.Value}");

    private static async Task<Result<T>> RunAsync<T>(Func<Task<T>> action) where T : notnull
    {
        try { return Result<T>.Success(await action()); }
        catch (ApiException exception) { return Result<T>.Failure(ToError(exception)); }
        catch (HttpRequestException exception) { return Result<T>.Failure(Error.Unexpected("api.network_error", exception.Message)); }
    }

    private static async Task<Result> RunCommandAsync(Func<Task> action)
    {
        try { await action(); return Result.Success(); }
        catch (ApiException exception) { return Result.Failure(ToError(exception)); }
        catch (HttpRequestException exception) { return Result.Failure(Error.Unexpected("api.network_error", exception.Message)); }
    }

    private static Error ToError(ApiException exception)
    {
        var details = exception.Errors.SelectMany(group => group.Value.Select(message => new ErrorDetail(group.Key, message))).ToArray();
        return exception.Kind switch
        {
            ApiErrorKind.Validation => Error.Validation("api.validation", exception.Detail, details),
            ApiErrorKind.NotFound => Error.NotFound("api.not_found", exception.Detail),
            ApiErrorKind.Conflict => Error.Conflict("api.conflict", exception.Detail, details),
            ApiErrorKind.Unauthorized => Error.Authentication("api.unauthorized", exception.Detail),
            ApiErrorKind.Forbidden => Error.Authorization("api.forbidden", exception.Detail),
            ApiErrorKind.BusinessRule => Error.BusinessRule("api.business_rule", exception.Detail, details),
            _ => Error.Unexpected("api.request_failed", exception.Detail)
        };
    }
}
