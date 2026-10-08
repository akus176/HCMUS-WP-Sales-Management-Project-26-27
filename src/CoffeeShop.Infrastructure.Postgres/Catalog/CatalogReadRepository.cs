namespace CoffeeShop.Infrastructure.Postgres.Catalog;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using Microsoft.EntityFrameworkCore;

public sealed class CatalogReadRepository : ICatalogReadRepository
{
    private readonly CoffeeShopDbContext context;

    public CatalogReadRepository(CoffeeShopDbContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<AddonSummary>> ListAddonsAsync(CancellationToken cancellationToken = default)
    {
        var addons = await context.Addons.AsNoTracking().OrderBy(addon => addon.Name).ThenBy(addon => addon.Id)
            .ToListAsync(cancellationToken);
        return addons.Select(addon => new AddonSummary(addon.Id, addon.Name, addon.Price.Amount, addon.IsActive)).ToArray();
    }

    public async Task<IReadOnlyList<RecipeIngredientOption>> ListActiveRecipeIngredientsAsync(CancellationToken cancellationToken = default) =>
        await context.Ingredients.AsNoTracking().Where(ingredient => ingredient.IsActive)
            .OrderBy(ingredient => ingredient.Name).ThenBy(ingredient => ingredient.Id)
            .Select(ingredient => new RecipeIngredientOption(ingredient.Id, ingredient.Name, ingredient.BaseUnit))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CategorySummary>> SearchCategoriesAsync(
        CategorySearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = context.Categories.AsNoTracking();
        if (criteria.Search is not null)
        {
            var search = criteria.Search.ToLowerInvariant();
            query = query.Where(category => category.Name.ToLower().Contains(search));
        }

        if (criteria.IsActive is not null)
        {
            query = query.Where(category => category.IsActive == criteria.IsActive);
        }

        return await query.OrderBy(category => category.DisplayOrder).ThenBy(category => category.Name)
            .ThenBy(category => category.Id).Take(criteria.Limit)
            .Select(category => new CategorySummary(category.Id, category.Name, category.DisplayOrder, category.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MenuItemSummary>> SearchMenuItemsAsync(
        MenuItemSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var query = context.MenuItems.AsNoTracking();
        if (criteria.Search is not null)
        {
            var search = criteria.Search.ToLowerInvariant();
            query = query.Where(item => item.Name.ToLower().Contains(search)
                || (item.Description != null && item.Description.ToLower().Contains(search)));
        }

        if (criteria.CategoryId is not null)
        {
            query = query.Where(item => item.CategoryId == criteria.CategoryId);
        }

        if (criteria.IsActive is not null)
        {
            query = query.Where(item => item.IsActive == criteria.IsActive);
        }

        var items = await (from item in query
                           join category in context.Categories.AsNoTracking() on item.CategoryId equals category.Id
                           orderby item.Name, item.Id
                           select new { Item = item, CategoryName = category.Name })
            .Take(criteria.Limit).ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return [];
        }

        var itemIds = items.Select(row => row.Item.Id).ToArray();
        var variants = await LoadVariantsAsync(itemIds, cancellationToken);
        var addons = await LoadAddonsAsync(itemIds, cancellationToken);
        return items.Select(row => new MenuItemSummary(
            row.Item.Id, row.Item.CategoryId, row.CategoryName, row.Item.Name, row.Item.Description, row.Item.IsActive,
            variants[row.Item.Id].Select(ToSummary).ToArray(), addons[row.Item.Id].ToArray())).ToArray();
    }

    public async Task<MenuItemDetail?> GetMenuItemDetailAsync(
        EntityId menuItemId,
        CancellationToken cancellationToken = default)
    {
        var row = await (from item in context.MenuItems.AsNoTracking()
                         join category in context.Categories.AsNoTracking() on item.CategoryId equals category.Id
                         where item.Id == menuItemId
                         select new { Item = item, CategoryName = category.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        var variants = (await LoadVariantsAsync([menuItemId], cancellationToken))[menuItemId].ToArray();
        var addons = await LoadAddonsAsync([menuItemId], cancellationToken);
        var recipes = await LoadRecipeLinesAsync(variants.Select(variant => variant.Id).ToArray(), cancellationToken);
        return new MenuItemDetail(
            row.Item.Id, row.Item.CategoryId, row.CategoryName, row.Item.Name, row.Item.Description, row.Item.IsActive,
            variants.Select(variant => new SizeVariantDetail(
                variant.Id, variant.SizeName, variant.Price.Amount, variant.IsActive, recipes[variant.Id].ToArray())).ToArray(),
            addons[menuItemId].ToArray());
    }

    public async Task<RecipeDetail?> GetRecipeAsync(
        EntityId sizeVariantId,
        CancellationToken cancellationToken = default)
    {
        var variant = await context.SizeVariants.AsNoTracking()
            .FirstOrDefaultAsync(variant => variant.Id == sizeVariantId, cancellationToken);
        if (variant is null)
        {
            return null;
        }

        var lines = await LoadRecipeLinesAsync([sizeVariantId], cancellationToken);
        return new RecipeDetail(variant.Id, variant.MenuItemId, variant.SizeName, lines[sizeVariantId].ToArray());
    }

    private async Task<ILookup<EntityId, SizeVariant>> LoadVariantsAsync(
        EntityId[] itemIds,
        CancellationToken cancellationToken) =>
        (await context.SizeVariants.AsNoTracking().Where(variant => itemIds.Contains(variant.MenuItemId))
            .OrderBy(variant => variant.SizeName).ThenBy(variant => variant.Id).ToListAsync(cancellationToken))
            .ToLookup(variant => variant.MenuItemId);

    private async Task<ILookup<EntityId, AddonSummary>> LoadAddonsAsync(
        EntityId[] itemIds,
        CancellationToken cancellationToken)
    {
        var rows = await (from link in context.MenuItemAddons.AsNoTracking()
                          join addon in context.Addons.AsNoTracking() on link.AddonId equals addon.Id
                          where itemIds.Contains(link.MenuItemId)
                          orderby addon.Name, addon.Id
                          select new { link.MenuItemId, Addon = addon }).ToListAsync(cancellationToken);
        return rows.ToLookup(row => row.MenuItemId,
            row => new AddonSummary(row.Addon.Id, row.Addon.Name, row.Addon.Price.Amount, row.Addon.IsActive));
    }

    private async Task<ILookup<EntityId, RecipeLineDetail>> LoadRecipeLinesAsync(
        EntityId[] variantIds,
        CancellationToken cancellationToken)
    {
        var rows = await (from line in context.RecipeIngredients.AsNoTracking()
                          join ingredient in context.Ingredients.AsNoTracking() on line.IngredientId equals ingredient.Id
                          where variantIds.Contains(line.SizeVariantId)
                          orderby ingredient.Name, line.Id
                          select new
                          {
                              line.SizeVariantId,
                              Detail = new RecipeLineDetail(line.Id, ingredient.Id, ingredient.Name, line.Quantity, line.Unit)
                          }).ToListAsync(cancellationToken);
        return rows.ToLookup(row => row.SizeVariantId, row => row.Detail);
    }

    private static SizeVariantSummary ToSummary(SizeVariant variant) =>
        new(variant.Id, variant.SizeName, variant.Price.Amount, variant.IsActive);
}
