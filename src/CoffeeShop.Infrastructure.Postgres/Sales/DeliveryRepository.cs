namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class DeliveryRepository(CoffeeShopDbContext context) : IDeliveryRepository
{
    public Task<Order?> FindOrderAsync(EntityId orderId, CancellationToken cancellationToken = default) => context.Orders.FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    public Task<Delivery?> FindDeliveryForOrderAsync(EntityId orderId, CancellationToken cancellationToken = default) => context.Deliveries.FirstOrDefaultAsync(delivery => delivery.OrderId == orderId, cancellationToken);
    public Task<bool> IsEligibleDeliveryStaffAsync(EntityId staffId, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(user => user.Id == staffId && user.IsActive && user.UserRoles.Any(role => role.Role!.Code == "DeliveryStaff"), cancellationToken);
    public void AddStatusHistory(DeliveryStatusHistory history) => context.DeliveryStatusHistories.Add(history);
}
