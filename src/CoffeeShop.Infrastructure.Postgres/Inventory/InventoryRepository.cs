namespace CoffeeShop.Infrastructure.Postgres.Inventory;

using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly CoffeeShopDbContext context;

    public InventoryRepository(CoffeeShopDbContext context)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Ingredients.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Supplier?> FindSupplierAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<PurchaseReceipt?> FindReceiptWithLinesAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.PurchaseReceipts
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Ingredient>> ListIngredientsAsync(CancellationToken cancellationToken = default) =>
        await context.Ingredients.ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Supplier>> ListSuppliersAsync(CancellationToken cancellationToken = default) =>
        await context.Suppliers.ToListAsync(cancellationToken);

    public void AddIngredient(Ingredient ingredient) => context.Ingredients.Add(ingredient);

    public void AddSupplier(Supplier supplier) => context.Suppliers.Add(supplier);

    public void AddReceipt(PurchaseReceipt receipt) => context.PurchaseReceipts.Add(receipt);
}
