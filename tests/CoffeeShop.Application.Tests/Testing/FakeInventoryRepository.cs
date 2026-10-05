namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Inventory;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public sealed class FakeInventoryRepository : IInventoryRepository
{
    public Dictionary<EntityId, Ingredient> Ingredients { get; } = [];

    public Dictionary<EntityId, Supplier> Suppliers { get; } = [];

    public Dictionary<EntityId, PurchaseReceipt> Receipts { get; } = [];

    public Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Ingredients.GetValueOrDefault(id));

    public Task<Supplier?> FindSupplierAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.GetValueOrDefault(id));

    public Task<PurchaseReceipt?> FindReceiptWithLinesAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Receipts.GetValueOrDefault(id));

    public Task<IReadOnlyList<Ingredient>> ListIngredientsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Ingredient>>(Ingredients.Values.ToList().AsReadOnly());

    public Task<IReadOnlyList<Supplier>> ListSuppliersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Supplier>>(Suppliers.Values.ToList().AsReadOnly());

    public void AddIngredient(Ingredient ingredient) => Ingredients.Add(ingredient.Id, ingredient);

    public void AddSupplier(Supplier supplier) => Suppliers.Add(supplier.Id, supplier);

    public void AddReceipt(PurchaseReceipt receipt) => Receipts.Add(receipt.Id, receipt);
}
