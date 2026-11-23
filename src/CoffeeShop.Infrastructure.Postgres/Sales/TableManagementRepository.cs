namespace CoffeeShop.Infrastructure.Postgres.Sales;

using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public sealed class TableManagementRepository(CoffeeShopDbContext context) : ITableManagementRepository
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
}
