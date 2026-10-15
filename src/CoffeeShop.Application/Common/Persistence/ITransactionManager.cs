namespace CoffeeShop.Application.Common.Persistence;

public interface ITransactionManager
{
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nested calls join the current transaction. The owning call rolls back when its
    /// action throws or returns a failed Result, including changes saved by nested calls.
    /// </summary>
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}
