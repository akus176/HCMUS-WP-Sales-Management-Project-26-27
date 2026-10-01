namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Common.Persistence;

public sealed class TestTransactionManager : ITransactionManager
{
    public int ExecutionCount { get; private set; }

    public int CommittedCount { get; private set; }

    public int RollbackCount { get; private set; }

    public bool ShouldFailTransaction { get; set; }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        ExecutionCount++;
        if (ShouldFailTransaction)
        {
            RollbackCount++;
            throw new InvalidOperationException("Simulated transaction failure.");
        }

        try
        {
            await action();
            CommittedCount++;
        }
        catch
        {
            RollbackCount++;
            throw;
        }
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();

        ExecutionCount++;
        if (ShouldFailTransaction)
        {
            RollbackCount++;
            throw new InvalidOperationException("Simulated transaction failure.");
        }

        try
        {
            var result = await action();
            CommittedCount++;
            return result;
        }
        catch
        {
            RollbackCount++;
            throw;
        }
    }
}
