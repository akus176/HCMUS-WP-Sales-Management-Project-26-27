namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public interface IOrderCancellationRepository
{
    Task<Order?> LoadOrderForCancellationAsync(EntityId orderId, CancellationToken cancellationToken = default);

    Task<DiningTable?> LoadTableForCancellationAsync(EntityId tableId, CancellationToken cancellationToken = default);
}
