namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class PosRepository(CoffeeShopDbContext context) : IPosRepository
{
    public async Task<PosOptions> GetOptionsAsync(CancellationToken cancellationToken = default) => new(
        await context.Users.AsNoTracking().Where(user => user.IsActive).OrderBy(user => user.FullName).ThenBy(user => user.Id)
            .Select(user => new PosStaffOption(user.Id, user.FullName)).ToArrayAsync(cancellationToken),
        await context.DiningTables.AsNoTracking().Where(table => !table.IsOccupied).OrderBy(table => table.TableNumber)
            .Select(table => new PosTableOption(table.Id, table.TableNumber, table.Seats)).ToArrayAsync(cancellationToken));

    public Task<bool> IsActiveStaffAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.Users.AnyAsync(user => user.Id == id && user.IsActive, cancellationToken);

    public async Task<PosOrderDetail?> GetOrderAsync(EntityId id, CancellationToken cancellationToken = default)
    {
        var order = await context.Orders.AsNoTracking().Include(order => order.Lines).ThenInclude(line => line.Addons)
            .Include(order => order.Discounts).AsSplitQuery().SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
        if (order is null) return null;
        var delivery = await context.Deliveries.AsNoTracking().SingleOrDefaultAsync(delivery => delivery.OrderId == id, cancellationToken);
        var customer = await context.Customers.Where(customer => customer.Id == order.CustomerId).Select(customer => customer.FullName).SingleOrDefaultAsync(cancellationToken);
        var table = await context.DiningTables.Where(table => table.Id == order.TableId).Select(table => table.TableNumber).SingleOrDefaultAsync(cancellationToken);
        return PosOrderDetail.From(order, delivery, customer, table);
    }

    public void AddDraft(Order order, Delivery? delivery)
    {
        context.Orders.Add(order);
        if (delivery is not null) context.Deliveries.Add(delivery);
    }
}
