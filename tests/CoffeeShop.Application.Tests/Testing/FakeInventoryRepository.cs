namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Inventory;

public sealed class FakeInventoryRepository : IInventoryRepository
{
    public Dictionary<EntityId, Ingredient> Ingredients { get; } = [];

    public Dictionary<EntityId, Supplier> Suppliers { get; } = [];

    public Dictionary<EntityId, PurchaseReceipt> Receipts { get; } = [];

    public List<StockMovement> StockMovements { get; } = [];

    public Dictionary<EntityId, OrderInventoryRestoration> OrderRestorations { get; } = [];

    public Task LockOrderForRestorationAsync(EntityId orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> HasOrderRestorationAsync(EntityId orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(OrderRestorations.ContainsKey(orderId));

    public Task<IReadOnlyList<Ingredient>> LoadIngredientsForRestorationAsync(
        IReadOnlyCollection<EntityId> ingredientIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Ingredient>>(Ingredients.Values.Where(ingredient => ingredientIds.Contains(ingredient.Id)).ToArray());

    public void AddOrderRestoration(OrderInventoryRestoration restoration) => OrderRestorations.Add(restoration.OrderId, restoration);

    public Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Ingredients.GetValueOrDefault(id));

    public Task<Ingredient?> LoadIngredientForAdjustmentAsync(EntityId id, CancellationToken cancellationToken = default) =>
        FindIngredientAsync(id, cancellationToken);

    public Task<StockMovement?> FindStockMovementAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(StockMovements.FirstOrDefault(movement => movement.Id == id));

    public Task<Supplier?> FindSupplierAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppliers.GetValueOrDefault(id));

    public Task<PurchaseReceipt?> FindReceiptWithLinesAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Receipts.GetValueOrDefault(id));

    public Task<IReadOnlyList<Ingredient>> ListIngredientsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Ingredient>>(Ingredients.Values.ToList().AsReadOnly());

    public Task<IReadOnlyList<Supplier>> ListSuppliersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Supplier>>(Suppliers.Values.ToList().AsReadOnly());

    public Task<IReadOnlyList<PurchaseReceipt>> ListReceiptsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PurchaseReceipt>>(Receipts.Values.ToList().AsReadOnly());

    public Task<IReadOnlyList<StockMovement>> ListMovementsForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StockMovement>>(StockMovements.Where(m => m.OrderId == orderId).ToList().AsReadOnly());

    public void AddIngredient(Ingredient ingredient) => Ingredients.Add(ingredient.Id, ingredient);

    public void AddSupplier(Supplier supplier) => Suppliers.Add(supplier.Id, supplier);

    public void AddReceipt(PurchaseReceipt receipt) => Receipts.Add(receipt.Id, receipt);

    public void AddStockMovement(StockMovement movement) => StockMovements.Add(movement);
}
