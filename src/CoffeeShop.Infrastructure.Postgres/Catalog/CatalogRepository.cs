namespace CoffeeShop.Infrastructure.Postgres.Catalog;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

public sealed class CatalogRepository : ICatalogRepository
{
    private readonly CoffeeShopDbContext context;

    public CatalogRepository(CoffeeShopDbContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Category?> FindCategoryAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public Task<MenuItem?> FindMenuItemAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.MenuItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<SizeVariant?> FindSizeVariantAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.SizeVariants.FirstOrDefaultAsync(variant => variant.Id == id, cancellationToken);

    public Task<Addon?> FindAddonAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Addons.FirstOrDefaultAsync(addon => addon.Id == id, cancellationToken);

    public Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Ingredients.FirstOrDefaultAsync(ingredient => ingredient.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SizeVariant>> ListSizeVariantsAsync(
        EntityId menuItemId,
        CancellationToken cancellationToken = default) =>
        await context.SizeVariants.Where(variant => variant.MenuItemId == menuItemId)
            .OrderBy(variant => variant.SizeName).ThenBy(variant => variant.Id)
            .ToListAsync(cancellationToken);

    public void AddCategory(Category category) => context.Categories.Add(category);

    public void AddMenuItem(MenuItem menuItem) => context.MenuItems.Add(menuItem);

    public void AddSizeVariant(SizeVariant sizeVariant) => context.SizeVariants.Add(sizeVariant);

    public void AddAddon(Addon addon) => context.Addons.Add(addon);

    public void ReplaceCompatibleAddons(EntityId menuItemId, IReadOnlyCollection<MenuItemAddon> compatibility)
    {
        RequireTransaction();
        // Lock the parent before reading so even initially empty sets serialize.
        context.Database.ExecuteSqlInterpolated($"SELECT 1 FROM catalog.menu_items WHERE id = {menuItemId.Value} FOR UPDATE");
        var existing = context.MenuItemAddons.Where(link => link.MenuItemId == menuItemId).ToList();
        var requestedIds = compatibility.Select(link => link.AddonId).ToHashSet();
        var existingIds = existing.Select(link => link.AddonId).ToHashSet();

        // Retain overlapping composite keys instead of tracking two objects with the same key.
        context.MenuItemAddons.RemoveRange(existing.Where(link => !requestedIds.Contains(link.AddonId)));
        context.MenuItemAddons.AddRange(compatibility.Where(link => !existingIds.Contains(link.AddonId)));
    }

    public void ReplaceRecipe(EntityId sizeVariantId, IReadOnlyCollection<RecipeIngredient> recipe)
    {
        RequireTransaction();
        context.Database.ExecuteSqlInterpolated($"SELECT 1 FROM catalog.size_variants WHERE id = {sizeVariantId.Value} FOR UPDATE");
        var existing = context.RecipeIngredients.Where(line => line.SizeVariantId == sizeVariantId).ToList();
        context.RecipeIngredients.RemoveRange(existing);
        context.RecipeIngredients.AddRange(recipe);
    }

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Catalog replacements require a transaction spanning replacement and save.");
        }
    }
}
