namespace CoffeeShop.Application.Inventory;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public interface IStockMovementReader
{
    Task<IReadOnlyList<StockMovement>> ListRecentAsync(EntityId ingredientId, CancellationToken cancellationToken = default);
}
