namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Domain.Common;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Catalog;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;

public class PostgresAdapterTests
{
    private static CoffeeShopDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>()
            .UseNpgsql("Host=localhost;Database=coffeeshop_test")
            .Options;

        return new CoffeeShopDbContext(options);
    }

    [Fact]
    public void CoffeeShopDbContext_ImplementsIUnitOfWork()
    {
        using var context = CreateContext();

        Assert.IsAssignableFrom<IUnitOfWork>(context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CatalogReplacement_WithoutTransaction_RejectsBeforeAccessingDatabase(bool recipe)
    {
        using var context = CreateContext();
        var repository = new CatalogRepository(context);

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            if (recipe)
            {
                repository.ReplaceRecipe(EntityId.New(), []);
            }
            else
            {
                repository.ReplaceCompatibleAddons(EntityId.New(), []);
            }
        });

        Assert.Contains("require a transaction", exception.Message, StringComparison.Ordinal);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public void PostgresTransactionManager_Constructor_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PostgresTransactionManager(null!));
    }

    [Fact]
    public async Task PostgresTransactionManager_NullAction_ThrowsArgumentNullException()
    {
        using var context = CreateContext();
        var manager = new PostgresTransactionManager(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.ExecuteInTransactionAsync((Func<Task>)null!));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            manager.ExecuteInTransactionAsync((Func<Task<int>>)null!));
    }
}
