namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed class TableManagementRepository(CoffeeShopDbContext context) :
    ITableManagementRepository, ITableManagementWorkspaceRepository
{
    public async Task<bool> CreateAsync(DiningTable table, CancellationToken cancellationToken = default)
    {
        context.DiningTables.Add(table);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_tables_table_number" })
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<TableManagementSummary>> ListTablesAsync(
        CancellationToken cancellationToken = default)
    {
        var tables = await context.DiningTables.AsNoTracking()
            .OrderBy(table => table.TableNumber)
            .ToArrayAsync(cancellationToken);
        var activeOrderIds = tables.Where(table => table.ActiveOrderId is not null)
            .Select(table => table.ActiveOrderId!)
            .Distinct()
            .ToArray();
        var orderNumbers = activeOrderIds.Length == 0
            ? new Dictionary<EntityId, string>()
            : await context.Orders.AsNoTracking()
                .Where(order => activeOrderIds.Contains(order.Id))
                .Select(order => new { order.Id, order.OrderNumber })
                .ToDictionaryAsync(order => order.Id, order => order.OrderNumber, cancellationToken);

        return tables.Select(table => new TableManagementSummary(table.Id, table.TableNumber, table.Seats,
            table.IsOccupied, table.ActiveOrderId is { } orderId && orderNumbers.TryGetValue(orderId, out var number)
                ? number : null)).ToArray();
    }

    public Task<DiningTable?> FindTableAsync(EntityId id, CancellationToken cancellationToken = default) =>
        context.DiningTables.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);

    public Task<bool> TableNumberExistsAsync(string tableNumber, EntityId? exceptId,
        CancellationToken cancellationToken = default)
    {
        var query = context.DiningTables.AsNoTracking().Where(table => table.TableNumber == tableNumber);
        if (exceptId is { } id) query = query.Where(table => table.Id != id);
        return query.AnyAsync(cancellationToken);
    }

    public void AddTable(DiningTable table) => context.DiningTables.Add(table);

    public void RemoveTable(DiningTable table) => context.DiningTables.Remove(table);
}
