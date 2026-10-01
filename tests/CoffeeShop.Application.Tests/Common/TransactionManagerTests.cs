namespace CoffeeShop.Application.Tests.Common;

using CoffeeShop.Application.Tests.Testing;

public class TransactionManagerTests
{
    [Fact]
    public async Task FakeUnitOfWork_SaveChangesAsync_IncrementsCallCount()
    {
        var uow = new FakeUnitOfWork();

        var result = await uow.SaveChangesAsync();

        Assert.Equal(1, result);
        Assert.Equal(1, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task FakeUnitOfWork_ShouldThrowOnSave_ThrowsConfiguredException()
    {
        var uow = new FakeUnitOfWork
        {
            ShouldThrowOnSave = true,
            ExceptionToThrow = new ApplicationException("DB error")
        };

        await Assert.ThrowsAsync<ApplicationException>(() => uow.SaveChangesAsync());
        Assert.Equal(0, uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task FakeUnitOfWork_CancelledToken_ThrowsOperationCanceledException()
    {
        var uow = new FakeUnitOfWork();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => uow.SaveChangesAsync(cts.Token));
    }

    [Fact]
    public async Task TestTransactionManager_ExecutesActionAndIncrementsCounts()
    {
        var manager = new TestTransactionManager();
        var executed = false;

        await manager.ExecuteInTransactionAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.True(executed);
        Assert.Equal(1, manager.ExecutionCount);
        Assert.Equal(1, manager.CommittedCount);
        Assert.Equal(0, manager.RollbackCount);
    }

    [Fact]
    public async Task TestTransactionManager_GenericOverload_ReturnsResult()
    {
        var manager = new TestTransactionManager();

        var result = await manager.ExecuteInTransactionAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
        Assert.Equal(1, manager.ExecutionCount);
        Assert.Equal(1, manager.CommittedCount);
        Assert.Equal(0, manager.RollbackCount);
    }

    [Fact]
    public async Task TestTransactionManager_WhenActionThrows_RollsBackAndRethrows()
    {
        var manager = new TestTransactionManager();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ExecuteInTransactionAsync(() => throw new InvalidOperationException("Action failed")));

        Assert.Equal(1, manager.ExecutionCount);
        Assert.Equal(0, manager.CommittedCount);
        Assert.Equal(1, manager.RollbackCount);
    }

    [Fact]
    public async Task TestTransactionManager_WhenShouldFail_RollsBackWithoutRunningAction()
    {
        var manager = new TestTransactionManager { ShouldFailTransaction = true };
        var executed = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.ExecuteInTransactionAsync(() =>
            {
                executed = true;
                return Task.CompletedTask;
            }));

        Assert.False(executed);
        Assert.Equal(1, manager.ExecutionCount);
        Assert.Equal(0, manager.CommittedCount);
        Assert.Equal(1, manager.RollbackCount);
    }

    [Fact]
    public async Task TestTransactionManager_NullAction_ThrowsArgumentNullException()
    {
        var manager = new TestTransactionManager();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.ExecuteInTransactionAsync((Func<Task>)null!));
    }
}
