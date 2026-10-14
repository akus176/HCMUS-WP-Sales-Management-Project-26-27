namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class OrderConfirmationRepository : IOrderConfirmationRepository
{
    private readonly CoffeeShopDbContext context;

    public OrderConfirmationRepository(CoffeeShopDbContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Order?> LoadOrderForConfirmationAsync(
        EntityId orderId,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        // Serialize confirmations of the same order before reading its lines.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.orders WHERE id = {orderId.Value} FOR UPDATE", cancellationToken);
        return await context.Orders
            .Include(order => order.Lines)
            .ThenInclude(line => line.Addons)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public async Task<DiningTable?> LoadTableForConfirmationAsync(
        EntityId tableId,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.tables WHERE id = {tableId.Value} FOR UPDATE", cancellationToken);
        return await context.DiningTables.FirstOrDefaultAsync(table => table.Id == tableId, cancellationToken);
    }

    public async Task<OrderCatalogSnapshot> LoadCatalogForConfirmationAsync(
        IReadOnlyCollection<EntityId> menuItemIds,
        IReadOnlyCollection<EntityId> sizeVariantIds,
        IReadOnlyCollection<EntityId> addonIds,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        var menuItems = await context.MenuItems
            .Where(item => menuItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var categoryIds = menuItems.Select(item => item.CategoryId).Distinct().ToArray();
        var categories = await context.Categories
            .Where(category => categoryIds.Contains(category.Id))
            .ToListAsync(cancellationToken);
        var variants = await context.SizeVariants
            .Where(variant => sizeVariantIds.Contains(variant.Id))
            .ToListAsync(cancellationToken);
        var addons = await context.Addons
            .Where(addon => addonIds.Contains(addon.Id))
            .ToListAsync(cancellationToken);
        var compatibility = await context.MenuItemAddons
            .Where(link => menuItemIds.Contains(link.MenuItemId))
            .ToListAsync(cancellationToken);

        return new OrderCatalogSnapshot(
            menuItems.ToDictionary(item => item.Id),
            categories.ToDictionary(category => category.Id),
            variants.ToDictionary(variant => variant.Id),
            addons.ToDictionary(addon => addon.Id),
            compatibility);
    }

    public async Task<IReadOnlyList<RecipeIngredient>> LoadRecipesAsync(
        IReadOnlyCollection<EntityId> sizeVariantIds,
        CancellationToken cancellationToken = default) =>
        await context.RecipeIngredients
            .Where(line => sizeVariantIds.Contains(line.SizeVariantId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Ingredient>> LoadIngredientsForConsumptionAsync(
        IReadOnlyCollection<EntityId> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        // Deterministic lock order avoids deadlocks between concurrent confirmations.
        var ids = ingredientIds.Select(id => id.Value).OrderBy(id => id).ToArray();
        return await context.Ingredients.FromSqlInterpolated(
            $"SELECT * FROM inventory.ingredients WHERE id = ANY({ids}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
    }

    public void AddStockMovement(StockMovement movement) => context.StockMovements.Add(movement);

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Order confirmation requires an explicit database transaction.");
        }
    }
}
