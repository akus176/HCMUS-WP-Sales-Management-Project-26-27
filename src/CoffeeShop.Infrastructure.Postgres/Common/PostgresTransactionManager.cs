namespace CoffeeShop.Infrastructure.Postgres.Common;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using Microsoft.EntityFrameworkCore;

public sealed class PostgresTransactionManager : ITransactionManager
{
    private readonly CoffeeShopDbContext _context;

    public PostgresTransactionManager(CoffeeShopDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                await action();
                return;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await action();
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                return await action();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action();
                if (result is Result { IsFailure: true })
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    _context.ChangeTracker.Clear();
                    return result;
                }

                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        });
    }
}
