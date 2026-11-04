namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public interface IInventoryRepository
{
    Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<Ingredient?> LoadIngredientForAdjustmentAsync(EntityId id, CancellationToken cancellationToken = default);

    Task LockAdjustmentRequestAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<StockMovement?> FindStockMovementAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<Supplier?> FindSupplierAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<PurchaseReceipt?> FindReceiptWithLinesAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ingredient>> ListIngredientsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Supplier>> ListSuppliersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseReceipt>> ListReceiptsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovement>> ListMovementsForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default);

    Task LockOrderForRestorationAsync(EntityId orderId, CancellationToken cancellationToken = default);

    Task<bool> HasOrderRestorationAsync(EntityId orderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ingredient>> LoadIngredientsForRestorationAsync(
        IReadOnlyCollection<EntityId> ingredientIds, CancellationToken cancellationToken = default);

    void AddOrderRestoration(OrderInventoryRestoration restoration);

    void AddIngredient(Ingredient ingredient);

    void AddSupplier(Supplier supplier);

    void AddReceipt(PurchaseReceipt receipt);

    void AddStockMovement(StockMovement movement);
}
