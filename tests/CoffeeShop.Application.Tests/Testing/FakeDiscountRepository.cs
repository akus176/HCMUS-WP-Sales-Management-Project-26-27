namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class FakeDiscountRepository : IDiscountRepository
{
    public Dictionary<EntityId, Order> Orders { get; } = [];
    public Dictionary<EntityId, EntityId> OtherDiscounts { get; } = [];
    public int OrderLockCount { get; private set; }

    public void AddDiscount(Discount discount)
    {
        // The order owns the request; the production adapter explicitly tracks it as new.
    }

    public Task<Order?> LoadOrderForDiscountUpdateAsync(EntityId orderId, CancellationToken cancellationToken = default)
    {
        OrderLockCount++;
        return Task.FromResult(Orders.GetValueOrDefault(orderId));
    }

    public Task<EntityId?> FindDiscountOrderIdAsync(EntityId discountId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Orders.Values.FirstOrDefault(order => order.Discounts.Any(discount => discount.Id == discountId))?.Id
            ?? OtherDiscounts.GetValueOrDefault(discountId));
}
