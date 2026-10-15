namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public interface IPosRepository
{
    Task<PosOptions> GetOptionsAsync(CancellationToken cancellationToken = default);
    Task<bool> IsActiveStaffAsync(EntityId id, CancellationToken cancellationToken = default);
    Task<PosOrderDetail?> GetOrderAsync(EntityId id, CancellationToken cancellationToken = default);
    void AddDraft(Order order, Delivery? delivery);
}
