namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class DeliveryRepository(CoffeeShopDbContext context) : IDeliveryRepository
{
    public async Task<Order?> LoadOrderForUpdateAsync(EntityId orderId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Delivery updates require an explicit database transaction.");
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.orders WHERE id = {orderId.Value} FOR UPDATE", cancellationToken);
        var order = await context.Orders.FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
        if (order is not null)
        {
            await context.Entry(order).ReloadAsync(cancellationToken);
        }

        return order;
    }

    public async Task<Delivery?> FindDeliveryForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default)
    {
        var delivery = await context.Deliveries.FirstOrDefaultAsync(delivery => delivery.OrderId == orderId, cancellationToken);
        if (delivery is not null)
        {
            await context.Entry(delivery).ReloadAsync(cancellationToken);
        }

        return delivery;
    }
    public Task<bool> IsEligibleDeliveryStaffAsync(EntityId staffId, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(user => user.Id == staffId && user.IsActive && user.UserRoles.Any(role => role.Role!.Code == "DeliveryStaff"), cancellationToken);
    public void AddStatusHistory(DeliveryStatusHistory history) => context.DeliveryStatusHistories.Add(history);
}
