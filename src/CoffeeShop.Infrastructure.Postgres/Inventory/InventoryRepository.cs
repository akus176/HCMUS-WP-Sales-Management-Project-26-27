namespace CoffeeShop.Infrastructure.Postgres.Inventory;

using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

public sealed class InventoryRepository : IInventoryRepository, IStockMovementReader
{
    private readonly CoffeeShopDbContext context;

    public InventoryRepository(CoffeeShopDbContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Ingredients.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<Ingredient?> LoadIngredientForAdjustmentAsync(EntityId id, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        return await context.Ingredients.FromSqlInterpolated(
            $"SELECT * FROM inventory.ingredients WHERE id = {id.Value} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task LockAdjustmentRequestAsync(EntityId id, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(142, hashtext({id.Value.ToString()}))", cancellationToken);
    }

    public Task<StockMovement?> FindStockMovementAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.StockMovements.AsNoTracking().FirstOrDefaultAsync(movement => movement.Id == id, cancellationToken);

    public Task<Supplier?> FindSupplierAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<PurchaseReceipt?> FindReceiptWithLinesAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.PurchaseReceipts
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<PurchaseReceipt?> LoadReceiptForReceivingAsync(string receiptNumber, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(143, hashtext({receiptNumber}))", cancellationToken);
        return await context.PurchaseReceipts.Include(receipt => receipt.Lines)
            .FirstOrDefaultAsync(receipt => receipt.ReceiptNumber == receiptNumber, cancellationToken);
    }

    public async Task<PurchaseReceipt?> LoadReceiptForPostingAsync(EntityId id, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT id FROM inventory.purchase_receipts WHERE id = {id.Value} FOR UPDATE", cancellationToken);
        var receipt = await FindReceiptWithLinesAsync(id, cancellationToken);
        if (receipt is not null)
            await context.Entry(receipt).ReloadAsync(cancellationToken);
        return receipt;
    }

    public async Task<IReadOnlyList<Ingredient>> ListIngredientsAsync(CancellationToken cancellationToken = default) =>
        await context.Ingredients.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Supplier>> ListSuppliersAsync(CancellationToken cancellationToken = default) =>
        await context.Suppliers.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PurchaseReceipt>> ListReceiptsAsync(CancellationToken cancellationToken = default) =>
        await context.PurchaseReceipts.Include(r => r.Lines).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockMovement>> ListMovementsForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default) =>
        await context.StockMovements
            .Where(m => m.OrderId == orderId)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(cancellationToken);

    public void AddIngredient(Ingredient ingredient) => context.Ingredients.Add(ingredient);

    public void AddSupplier(Supplier supplier) => context.Suppliers.Add(supplier);

    public void AddReceipt(PurchaseReceipt receipt) => context.PurchaseReceipts.Add(receipt);

    public void AddStockMovement(StockMovement movement) => context.StockMovements.Add(movement);

    public async Task<IReadOnlyList<StockMovement>> ListRecentAsync(EntityId ingredientId, CancellationToken cancellationToken = default) =>
        await context.StockMovements.AsNoTracking().Where(movement => movement.IngredientId == ingredientId)
            .OrderByDescending(movement => movement.OccurredAt).ThenBy(movement => movement.Id)
            .Take(100).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<StockMovement>> ListRecentForAllAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 500);
        return await context.StockMovements.AsNoTracking()
            .OrderByDescending(movement => movement.OccurredAt).ThenBy(movement => movement.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
    }

    public async Task LockOrderForRestorationAsync(EntityId orderId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.orders WHERE id = {orderId.Value} FOR UPDATE", cancellationToken);
    }

    public Task<bool> HasOrderRestorationAsync(EntityId orderId, CancellationToken cancellationToken = default) =>
        context.OrderInventoryRestorations.AnyAsync(restoration => restoration.OrderId == orderId, cancellationToken);

    public async Task<IReadOnlyList<Ingredient>> LoadIngredientsForStockUpdateAsync(
        IReadOnlyCollection<EntityId> ingredientIds, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        var ids = ingredientIds.Select(id => id.Value).OrderBy(id => id).ToArray();
        var ingredients = await context.Ingredients.FromSqlInterpolated(
            $"SELECT * FROM inventory.ingredients WHERE id = ANY({ids}) ORDER BY id FOR UPDATE")
            .ToListAsync(cancellationToken);
        // Refresh entities a long-lived scope may have read before acquiring the locks.
        foreach (var ingredient in ingredients)
        {
            await context.Entry(ingredient).ReloadAsync(cancellationToken);
        }

        return ingredients;
    }

    public void AddOrderRestoration(OrderInventoryRestoration restoration) => context.OrderInventoryRestorations.Add(restoration);

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Order inventory restoration requires an explicit database transaction.");
        }
    }
}
