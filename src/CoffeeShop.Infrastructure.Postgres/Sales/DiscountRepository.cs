namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class DiscountRepository(CoffeeShopDbContext context) : IDiscountRepository
{
    public void AddDiscount(Discount discount) => context.Discounts.Add(discount);

    public async Task<Order?> LoadOrderForDiscountUpdateAsync(
        EntityId orderId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Discount updates require an explicit database transaction.");
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.orders WHERE id = {orderId.Value} FOR UPDATE", cancellationToken);
        return await context.Orders.Include(order => order.Discounts)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public Task<EntityId?> FindDiscountOrderIdAsync(
        EntityId discountId, CancellationToken cancellationToken = default) =>
        context.Discounts.AsNoTracking().Where(discount => discount.Id == discountId)
            .Select(discount => (EntityId?)discount.OrderId).FirstOrDefaultAsync(cancellationToken);
}
