namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public interface IDeliveryRepository
{
    Task<Order?> LoadOrderForUpdateAsync(EntityId orderId, CancellationToken cancellationToken = default);
    Task<Delivery?> FindDeliveryForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default);
    Task<bool> IsEligibleDeliveryStaffAsync(EntityId staffId, CancellationToken cancellationToken = default);
    void AddStatusHistory(DeliveryStatusHistory history);
}
