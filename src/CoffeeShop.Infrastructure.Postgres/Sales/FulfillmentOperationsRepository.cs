namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class FulfillmentOperationsRepository(CoffeeShopDbContext context) : IFulfillmentOperationsRepository, IOrderFulfillmentRepository
{
    public async Task<IReadOnlyList<OperationalOrder>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await context.Orders.AsNoTracking().Include(order => order.Discounts).Include(order => order.Payments)
            .OrderBy(order => order.CreatedAt).ToArrayAsync(cancellationToken);
        var tables = await context.DiningTables.AsNoTracking().ToDictionaryAsync(table => table.Id, cancellationToken);
        var deliveries = await context.Deliveries.AsNoTracking().ToDictionaryAsync(delivery => delivery.OrderId, cancellationToken);
        return orders.Select(order =>
        {
            deliveries.TryGetValue(order.Id, out var delivery);
            return new OperationalOrder(order.Id, order.OrderNumber, order.Channel, order.Status, order.Total.Amount,
                order.PaidAmount.Amount, order.TableId is not null && tables.TryGetValue(order.TableId, out var table) ? table.TableNumber : null,
                delivery?.Status, delivery?.AssignedStaffId, order.Discounts.Where(discount => discount.Status == DiscountStatus.Pending).Select(discount => discount.Id).ToArray());
        }).ToArray();
    }

    public async Task<IReadOnlyList<OperationalTable>> GetTablesAsync(CancellationToken cancellationToken = default) =>
        await context.DiningTables.AsNoTracking().OrderBy(table => table.TableNumber)
            .Select(table => new OperationalTable(table.Id, table.TableNumber, table.Seats, table.IsOccupied, table.ActiveOrderId))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<DeliveryStaffOption>> GetDeliveryStaffAsync(CancellationToken cancellationToken = default) =>
        await context.Users.AsNoTracking().Where(user => user.IsActive && user.UserRoles.Any(role => role.Role!.Code == "DeliveryStaff"))
            .OrderBy(user => user.FullName).Select(user => new DeliveryStaffOption(user.Id, user.FullName)).ToArrayAsync(cancellationToken);

    public async Task<Order?> LoadOrderForUpdateAsync(EntityId orderId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM sales.orders WHERE id = {orderId.Value} FOR UPDATE", cancellationToken);
        return await context.Orders.Include(order => order.Payments).FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public async Task<DiningTable?> LoadTableForUpdateAsync(EntityId tableId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM sales.tables WHERE id = {tableId.Value} FOR UPDATE", cancellationToken);
        return await context.DiningTables.FirstOrDefaultAsync(table => table.Id == tableId, cancellationToken);
    }

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Fulfillment updates require an explicit database transaction.");
    }
}
