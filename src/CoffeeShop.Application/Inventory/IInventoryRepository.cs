namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public interface IInventoryRepository
{
    Task<Ingredient?> FindIngredientAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<Supplier?> FindSupplierAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<PurchaseReceipt?> FindReceiptWithLinesAsync(EntityId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Ingredient>> ListIngredientsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Supplier>> ListSuppliersAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseReceipt>> ListReceiptsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockMovement>> ListMovementsForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default);

    void AddIngredient(Ingredient ingredient);

    void AddSupplier(Supplier supplier);

    void AddReceipt(PurchaseReceipt receipt);

    void AddStockMovement(StockMovement movement);
}
