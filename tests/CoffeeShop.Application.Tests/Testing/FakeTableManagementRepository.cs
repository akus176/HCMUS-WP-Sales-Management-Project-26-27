using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

namespace CoffeeShop.Application.Tests.Testing;

public sealed class FakeTableManagementRepository : ITableManagementRepository
{
    public List<DiningTable> Tables { get; } = [];

    public Task<IReadOnlyList<TableManagementSummary>> ListTablesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TableManagementSummary>>(Tables
            .Select(table => new TableManagementSummary(table.Id, table.TableNumber, table.Seats, table.IsOccupied,
                null))
            .ToArray());

    public Task<DiningTable?> FindTableAsync(EntityId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Tables.SingleOrDefault(table => table.Id == id));

    public Task<bool> TableNumberExistsAsync(string tableNumber, EntityId? exceptId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(Tables.Any(table => table.TableNumber == tableNumber &&
            (exceptId is null || table.Id != exceptId)));

    public void AddTable(DiningTable table) => Tables.Add(table);

    public void RemoveTable(DiningTable table) => Tables.Remove(table);
}
