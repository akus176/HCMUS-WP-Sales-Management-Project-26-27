namespace CoffeeShop.Application.Sales;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public interface IDiscountRepository
{
    void AddDiscount(Discount discount);

    Task<Order?> LoadOrderForDiscountUpdateAsync(EntityId orderId, CancellationToken cancellationToken = default);

    Task<EntityId?> FindDiscountOrderIdAsync(EntityId discountId, CancellationToken cancellationToken = default);
}
