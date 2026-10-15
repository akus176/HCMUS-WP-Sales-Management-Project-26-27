namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;

public sealed class OrderCancellationRepository(CoffeeShopDbContext context) : IOrderCancellationRepository
{
    public async Task<Order?> LoadOrderForCancellationAsync(EntityId orderId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.orders WHERE id = {orderId.Value} FOR UPDATE", cancellationToken);
        var order = await context.Orders.FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
        if (order is not null)
        {
            await context.Entry(order).ReloadAsync(cancellationToken);
        }

        return order;
    }

    public async Task<DiningTable?> LoadTableForCancellationAsync(EntityId tableId, CancellationToken cancellationToken = default)
    {
        RequireTransaction();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM sales.tables WHERE id = {tableId.Value} FOR UPDATE", cancellationToken);
        var table = await context.DiningTables.FirstOrDefaultAsync(table => table.Id == tableId, cancellationToken);
        if (table is not null)
        {
            await context.Entry(table).ReloadAsync(cancellationToken);
        }

        return table;
    }

    private void RequireTransaction()
    {
        if (context.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Order cancellation requires an explicit database transaction.");
        }
    }
}
