namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Common.Persistence;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public bool ShouldThrowOnSave { get; set; }

    public Exception? ExceptionToThrow { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ShouldThrowOnSave)
        {
            throw ExceptionToThrow ?? new InvalidOperationException("Simulated unit of work persistence failure.");
        }

        SaveChangesCallCount++;
        return Task.FromResult(1);
    }
}
