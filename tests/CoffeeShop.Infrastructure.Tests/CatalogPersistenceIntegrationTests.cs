namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class CatalogPersistenceIntegrationTests : IAsyncLifetime
{
    public const string ConnectionVariable = "COFFEESHOP_TEST_POSTGRES";
    private readonly string databaseName = $"coffeeshop_catalog_{Guid.NewGuid():N}";
    private ServiceProvider services = null!;
    private string adminConnectionString = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        adminConnectionString = configured;
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(configured) { Database = databaseName, Pooling = false };
        services = new ServiceCollection().AddCoffeeShopPostgres(builder.ConnectionString).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (services is not null)
        {
            await services.DisposeAsync();
        }

        if (adminConnectionString is not null)
        {
            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [PostgresFact]
    public async Task ReservationMigration_UpgradesTheInitialSchemaAndRebuildsTheFilteredIndex()
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var migrations = context.Database.GetMigrations().ToArray();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[0]);
        var original = await IndexDefinitionAsync(context);
        Assert.DoesNotContain("UNIQUE", original, StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE", original, StringComparison.Ordinal);

        await migrator.MigrateAsync();

        var updated = await IndexDefinitionAsync(context);
        Assert.Contains("UNIQUE", updated, StringComparison.Ordinal);
        Assert.Contains("WHERE", updated, StringComparison.Ordinal);
        Assert.Contains("cancelled", updated, StringComparison.Ordinal);
        Assert.Contains("completed", updated, StringComparison.Ordinal);
        Assert.Equal(migrations, await context.Database.GetAppliedMigrationsAsync());
    }

    [PostgresFact]
    public async Task CompleteDrink_SavesAndReloadsThroughApplicationServices()
    {
        var drink = await SeedDrinkAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            var queries = Queries(scope.ServiceProvider);
            var detail = (await queries.GetMenuItemDetailAsync(drink.Item)).Value;
            Assert.Equal("Coffee", detail.CategoryName);
            Assert.Equal("Latte", detail.Name);
            Assert.Equal("Milk coffee", detail.Description);
            Assert.Equal(2, detail.SizeVariants.Count);
            Assert.Equal(2, detail.CompatibleAddons.Count);
            var small = Assert.Single(detail.SizeVariants, variant => variant.Id == drink.Small);
            Assert.Equal(25.50m, small.Price);
            Assert.Equal(18.125m, Assert.Single(small.Recipe).Quantity);
            var large = Assert.Single(detail.SizeVariants, variant => variant.Id == drink.Large);
            Assert.Equal(35.75m, large.Price);
            Assert.Equal(2, large.Recipe.Count);
            Assert.Contains(large.Recipe, line => line.IngredientName == "Milk" && line.Quantity == 0.25m && line.Unit == "l");
            var recipe = (await queries.GetRecipeAsync(drink.Large)).Value;
            Assert.Equal(large.Recipe, recipe.Lines);
            Assert.Empty(scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().ChangeTracker.Entries());
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var handler = Commands(scope.ServiceProvider);
            Assert.True((await handler.UpdateCategoryAsync(new(drink.Category, "Espresso"))).IsSuccess);
            Assert.True((await handler.UpdateMenuItemAsync(new(drink.Item, drink.Category, "Iced latte", "Cold"))).IsSuccess);
            Assert.True((await handler.UpdateSizeVariantAsync(new(drink.Item, drink.Small, "Regular", 27m))).IsSuccess);
            Assert.True((await handler.UpdateAddonAsync(new(drink.Addon, "Extra shot", 6m))).IsSuccess);
            Assert.True((await handler.DeactivateAddonAsync(new(drink.Addon))).IsSuccess);
            Assert.True((await handler.DeactivateSizeVariantAsync(new(drink.Small))).IsSuccess);
            Assert.True((await handler.DeactivateMenuItemAsync(new(drink.Item))).IsSuccess);
            Assert.True((await handler.DeactivateCategoryAsync(new(drink.Category))).IsSuccess);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            var detail = (await Queries(scope.ServiceProvider).GetMenuItemDetailAsync(drink.Item)).Value;
            Assert.False(detail.IsActive);
            Assert.Equal("Espresso", detail.CategoryName);
            Assert.Equal("Iced latte", detail.Name);
            var small = Assert.Single(detail.SizeVariants, variant => variant.Id == drink.Small);
            Assert.False(small.IsActive);
            Assert.Equal("Regular", small.SizeName);
            Assert.Equal(27m, small.Price);
            Assert.Single(small.Recipe);
            var addon = Assert.Single(detail.CompatibleAddons, addon => addon.Id == drink.Addon);
            Assert.False(addon.IsActive);
            Assert.Equal("Extra shot", addon.Name);
            Assert.Equal(6m, addon.Price);
            Assert.Equal(100m, (await scope.ServiceProvider.GetRequiredService<ICatalogRepository>()
                .FindIngredientAsync(drink.Beans))!.AvailableQuantity);
        }
    }

    [PostgresFact]
    public async Task RecipeReplacement_OverlappingIngredientsAndRepeatedWritesRemainSizeSpecific()
    {
        var drink = await SeedDrinkAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            var handler = Recipes(scope.ServiceProvider);
            Assert.True((await handler.ReplaceRecipeAsync(new(drink.Large, [new(drink.Beans, 22m, "g")]))).IsSuccess);
            Assert.True((await handler.ReplaceRecipeAsync(new(drink.Large,
                [new(drink.Beans, 24m, "g"), new(drink.Milk, 0.3m, "l")]))).IsSuccess);
        }

        await using var readScope = services.CreateAsyncScope();
        var queries = Queries(readScope.ServiceProvider);
        var large = (await queries.GetRecipeAsync(drink.Large)).Value;
        Assert.Equal(2, large.Lines.Count);
        Assert.Contains(large.Lines, line => line.IngredientId == drink.Beans && line.Quantity == 24m);
        Assert.Contains(large.Lines, line => line.IngredientId == drink.Milk && line.Quantity == 0.3m);
        Assert.Equal(18.125m, Assert.Single((await queries.GetRecipeAsync(drink.Small)).Value.Lines).Quantity);
    }

    [PostgresFact]
    public async Task CompatibilityReplacement_RetainsOverlappingKeysAndAllowsClearing()
    {
        var drink = await SeedDrinkAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            var handler = Commands(scope.ServiceProvider);
            Assert.True((await handler.SetMenuItemAddonsAsync(new(drink.Item, [drink.Addon]))).IsSuccess);
            Assert.True((await handler.SetMenuItemAddonsAsync(new(drink.Item, [drink.Addon]))).IsSuccess);
        }

        await using (var scope = services.CreateAsyncScope())
        {
            Assert.Equal(drink.Addon, Assert.Single((await Queries(scope.ServiceProvider)
                .GetMenuItemDetailAsync(drink.Item)).Value.CompatibleAddons).Id);
            Assert.True((await Commands(scope.ServiceProvider).SetMenuItemAddonsAsync(new(drink.Item, []))).IsSuccess);
        }

        await using var readScope = services.CreateAsyncScope();
        Assert.Empty((await Queries(readScope.ServiceProvider).GetMenuItemDetailAsync(drink.Item)).Value.CompatibleAddons);
    }

    [PostgresTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConcurrentReplacements_SerializeBeforeReadingTheExistingSet(bool recipe, bool initiallyEmpty)
    {
        var drink = await SeedDrinkAsync();
        EntityId otherAddon;
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            otherAddon = await context.MenuItemAddons.Where(link => link.MenuItemId == drink.Item && link.AddonId != drink.Addon)
                .Select(link => link.AddonId).SingleAsync();
            if (initiallyEmpty)
            {
                if (recipe)
                {
                    context.RecipeIngredients.RemoveRange(context.RecipeIngredients.Where(line => line.SizeVariantId == drink.Large));
                }
                else
                {
                    Assert.True((await Commands(scope.ServiceProvider).SetMenuItemAddonsAsync(new(drink.Item, []))).IsSuccess);
                }

                await context.SaveChangesAsync();
            }
        }

        var firstSaved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondConnectionName = $"catalog_replace_{Guid.NewGuid():N}";
        await using var firstScope = services.CreateAsyncScope();
        await using var secondScope = services.CreateAsyncScope();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        secondContext.Database.SetConnectionString(new NpgsqlConnectionStringBuilder(secondContext.Database.GetConnectionString())
        {
            ApplicationName = secondConnectionName
        }.ConnectionString);

        var first = firstScope.ServiceProvider.GetRequiredService<ITransactionManager>().ExecuteInTransactionAsync(async () =>
        {
            var result = recipe
                ? await Recipes(firstScope.ServiceProvider).ReplaceRecipeAsync(new(drink.Large, [new(drink.Beans, 22m, "g")]))
                : await Commands(firstScope.ServiceProvider).SetMenuItemAddonsAsync(new(drink.Item, [drink.Addon]));
            Assert.True(result.IsSuccess);
            firstSaved.SetResult();
            await releaseFirst.Task.WaitAsync(TimeSpan.FromSeconds(15));
        });

        Task<Result>? second = null;
        var waitedForLock = false;
        try
        {
            await firstSaved.Task.WaitAsync(TimeSpan.FromSeconds(10));
            second = Task.Run(async () => recipe
                ? await Recipes(secondScope.ServiceProvider).ReplaceRecipeAsync(new(drink.Large, [new(drink.Milk, 200m, "ml")]))
                : await Commands(secondScope.ServiceProvider).SetMenuItemAddonsAsync(new(drink.Item, [otherAddon])));
            waitedForLock = await WaitForLockOrCompletionAsync(secondConnectionName, second);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await first;
            if (second is not null)
            {
                Assert.True((await second.WaitAsync(TimeSpan.FromSeconds(10))).IsSuccess);
            }
        }

        await using var readScope = services.CreateAsyncScope();
        if (recipe)
        {
            var line = Assert.Single((await Queries(readScope.ServiceProvider).GetRecipeAsync(drink.Large)).Value.Lines);
            Assert.Equal(drink.Milk, line.IngredientId);
            Assert.Equal(200m, line.Quantity);
        }
        else
        {
            var addon = Assert.Single((await Queries(readScope.ServiceProvider).GetMenuItemDetailAsync(drink.Item)).Value.CompatibleAddons);
            Assert.Equal(otherAddon, addon.Id);
        }

        Assert.True(waitedForLock, "The second replacement must wait until the first transaction commits.");
    }

    [PostgresTheory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("zero")]
    [InlineData("negative")]
    [InlineData("incompatible")]
    [InlineData("duplicate")]
    [InlineData("empty")]
    public async Task InvalidRecipe_PreservesTheEntirePersistedRecipe(string invalidCase)
    {
        var drink = await SeedDrinkAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            if (invalidCase == "inactive")
            {
                var ingredient = (await scope.ServiceProvider.GetRequiredService<ICatalogRepository>()
                    .FindIngredientAsync(drink.Milk))!;
                ingredient.Deactivate(new UtcTimestamp(DateTimeOffset.UtcNow));
                await context.SaveChangesAsync();
            }

            RecipeLineInput[] lines = invalidCase switch
            {
                "missing" => [new(drink.Beans, 99m, "g"), new(EntityId.New(), 1m, "g")],
                "inactive" => [new(drink.Beans, 99m, "g"), new(drink.Milk, 1m, "ml")],
                "zero" => [new(drink.Beans, 99m, "g"), new(drink.Milk, 0m, "ml")],
                "negative" => [new(drink.Beans, 99m, "g"), new(drink.Milk, -1m, "ml")],
                "incompatible" => [new(drink.Beans, 99m, "g"), new(drink.Milk, 1m, "kg")],
                "duplicate" => [new(drink.Beans, 99m, "g"), new(drink.Beans, 1m, "g")],
                _ => []
            };
            var result = await Recipes(scope.ServiceProvider).ReplaceRecipeAsync(new(drink.Large, lines));
            Assert.Equal(ErrorType.Validation, result.Error!.Type);
            Assert.False(context.ChangeTracker.HasChanges());
        }

        await using var readScope = services.CreateAsyncScope();
        var recipe = (await Queries(readScope.ServiceProvider).GetRecipeAsync(drink.Large)).Value;
        Assert.Equal(2, recipe.Lines.Count);
        Assert.Contains(recipe.Lines, line => line.IngredientId == drink.Beans && line.Quantity == 24m);
        Assert.Contains(recipe.Lines, line => line.IngredientId == drink.Milk && line.Quantity == 0.25m);
    }

    [PostgresFact]
    public async Task FailureAfterRecipeSave_RollsBackDeletionAndInsertion()
    {
        var drink = await SeedDrinkAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
            var variant = (await repository.FindSizeVariantAsync(drink.Large))!;
            var ingredient = (await repository.FindIngredientAsync(drink.Beans))!;
            var transaction = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => transaction.ExecuteInTransactionAsync(async () =>
            {
                repository.ReplaceRecipe(drink.Large, [new RecipeIngredient(EntityId.New(), variant, ingredient, 99m, "g")]);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
                throw new InvalidOperationException("Injected failure after save.");
            }));
        }

        await using var readScope = services.CreateAsyncScope();
        var recipe = (await Queries(readScope.ServiceProvider).GetRecipeAsync(drink.Large)).Value;
        Assert.Equal(2, recipe.Lines.Count);
        Assert.Contains(recipe.Lines, line => line.IngredientId == drink.Beans && line.Quantity == 24m);
        Assert.Contains(recipe.Lines, line => line.IngredientId == drink.Milk && line.Quantity == 0.25m);
    }

    [PostgresFact]
    public async Task Search_FiltersAndLimitsOnServerAndReturnsNoTrackedEntities()
    {
        var drink = await SeedDrinkAsync();
        EntityId otherCategory;
        await using (var scope = services.CreateAsyncScope())
        {
            var handler = Commands(scope.ServiceProvider);
            otherCategory = (await handler.CreateCategoryAsync(new("Tea", 1))).Value;
            Assert.True((await handler.CreateMenuItemAsync(new(otherCategory, "100%_Tea", null, [new("Cup", 10m)]))).IsSuccess);
            Assert.True((await handler.CreateMenuItemAsync(new(drink.Category, "Americano", null, [new("Cup", 15m)]))).IsSuccess);
            Assert.True((await handler.DeactivateMenuItemAsync(new(drink.Item))).IsSuccess);
        }

        await using var readScope = services.CreateAsyncScope();
        var queries = Queries(readScope.ServiceProvider);
        Assert.Equal(drink.Category, Assert.Single((await queries.SearchCategoriesAsync(new("COF"))).Value).Id);
        Assert.Equal(otherCategory, Assert.Single((await queries.SearchCategoriesAsync(new(Limit: 1))).Value).Id);
        Assert.Equal(drink.Item, Assert.Single((await queries.SearchMenuItemsAsync(new("LAT", IsActive: false))).Value).Id);
        Assert.Equal(drink.Item, Assert.Single((await queries.SearchMenuItemsAsync(new("MILK"))).Value).Id);
        Assert.Equal("100%_Tea", Assert.Single((await queries.SearchMenuItemsAsync(new("%_"))).Value).Name);
        Assert.Equal("Americano", Assert.Single((await queries.SearchMenuItemsAsync(new(CategoryId: drink.Category, IsActive: true))).Value).Name);
        Assert.Equal("100%_Tea", Assert.Single((await queries.SearchMenuItemsAsync(new(Limit: 1))).Value).Name);
        Assert.Empty((await queries.SearchMenuItemsAsync(new("not present"))).Value);
        Assert.Empty((await queries.SearchCategoriesAsync(new("not present"))).Value);
        Assert.Equal(ErrorType.NotFound, (await queries.GetMenuItemDetailAsync(EntityId.New())).Error!.Type);
        Assert.Equal(ErrorType.NotFound, (await queries.GetRecipeAsync(EntityId.New())).Error!.Type);
        Assert.Empty(readScope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().ChangeTracker.Entries());
    }

    [PostgresTheory]
    [InlineData("quantity", PostgresErrorCodes.CheckViolation)]
    [InlineData("ingredient", PostgresErrorCodes.ForeignKeyViolation)]
    [InlineData("duplicate", PostgresErrorCodes.UniqueViolation)]
    public async Task DatabaseConstraints_RejectInvalidRecipeRows(string invalidCase, string sqlState)
    {
        var drink = await SeedDrinkAsync();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var ingredientId = invalidCase == "ingredient" ? Guid.NewGuid() : drink.Beans.Value;
        var quantity = invalidCase == "quantity" ? 0m : 1m;
        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO catalog.recipe_ingredients (id, size_variant_id, ingredient_id, quantity, unit) VALUES ({Guid.NewGuid()}, {drink.Small.Value}, {ingredientId}, {quantity}, 'g')"));
        Assert.Equal(sqlState, exception.SqlState);
    }

    [PostgresFact]
    public async Task MissingRecordsAndEmptyRecipe_ReturnExpectedResults()
    {
        await using var scope = services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICatalogRepository>();
        var id = EntityId.New();
        Assert.Null(await repository.FindCategoryAsync(id));
        Assert.Null(await repository.FindMenuItemAsync(id));
        Assert.Null(await repository.FindSizeVariantAsync(id));
        Assert.Null(await repository.FindAddonAsync(id));
        Assert.Null(await repository.FindIngredientAsync(id));
        Assert.Empty(await repository.ListSizeVariantsAsync(id));
        var handler = Commands(scope.ServiceProvider);
        var category = (await handler.CreateCategoryAsync(new("Coffee"))).Value;
        var item = (await handler.CreateMenuItemAsync(new(category, "Espresso", null, [new("Cup", 0m)]))).Value;
        var variant = Assert.Single(await repository.ListSizeVariantsAsync(item));
        Assert.Empty((await Queries(scope.ServiceProvider).GetRecipeAsync(variant.Id)).Value.Lines);
    }

    [PostgresFact]
    public async Task CancelledRead_PropagatesCancellation()
    {
        await using var scope = services.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Queries(scope.ServiceProvider)
            .SearchMenuItemsAsync(cancellationToken: cancellation.Token));
    }

    private async Task<Drink> SeedDrinkAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var handler = Commands(provider);
        var category = (await handler.CreateCategoryAsync(new("Coffee", 2))).Value;
        var item = (await handler.CreateMenuItemAsync(new(category, "Latte", "Milk coffee",
            [new("Small", 25.50m), new("Large", 35.75m)]))).Value;
        var addon = (await handler.CreateAddonAsync(new("Shot", 5.50m))).Value;
        var secondAddon = (await handler.CreateAddonAsync(new("Cream", 3m))).Value;
        Assert.True((await handler.SetMenuItemAddonsAsync(new(item, [addon, secondAddon]))).IsSuccess);
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var beans = new Ingredient(EntityId.New(), "Beans", "g", now, now, 100m, 5m);
        var milk = new Ingredient(EntityId.New(), "Milk", "ml", now, now, 1000m, 100m);
        var context = provider.GetRequiredService<CoffeeShopDbContext>();
        context.Ingredients.AddRange(beans, milk);
        await context.SaveChangesAsync();
        var variants = await provider.GetRequiredService<ICatalogRepository>().ListSizeVariantsAsync(item);
        var small = Assert.Single(variants, variant => variant.SizeName == "Small").Id;
        var large = Assert.Single(variants, variant => variant.SizeName == "Large").Id;
        Assert.True((await Recipes(provider).ReplaceRecipeAsync(new(small, [new(beans.Id, 18.125m, "g")]))).IsSuccess);
        Assert.True((await Recipes(provider).ReplaceRecipeAsync(new(large,
            [new(beans.Id, 24m, "g"), new(milk.Id, 0.25m, "l")]))).IsSuccess);
        return new Drink(category, item, small, large, addon, beans.Id, milk.Id);
    }

    private static async Task<string> IndexDefinitionAsync(CoffeeShopDbContext context)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT indexdef FROM pg_indexes WHERE schemaname = 'sales' AND indexname = 'IX_reservations_table_id_reserved_for'";
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> WaitForLockOrCompletionAsync(string applicationName, Task replacement)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE application_name = @name AND wait_event_type = 'Lock')", connection);
        command.Parameters.AddWithValue("name", applicationName);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!replacement.IsCompleted)
        {
            if ((bool)(await command.ExecuteScalarAsync(timeout.Token))!)
            {
                return true;
            }

            await Task.Delay(25, timeout.Token);
        }

        return false;
    }

    private static CatalogCommandHandler Commands(IServiceProvider provider) => new(
        provider.GetRequiredService<ICatalogRepository>(), provider.GetRequiredService<IUnitOfWork>(),
        provider.GetRequiredService<ITransactionManager>(), new SystemClock());

    private static RecipeCommandHandler Recipes(IServiceProvider provider) => new(
        provider.GetRequiredService<ICatalogRepository>(), provider.GetRequiredService<IUnitOfWork>(),
        provider.GetRequiredService<ITransactionManager>());

    private static CatalogQueryHandler Queries(IServiceProvider provider) => new(provider.GetRequiredService<ICatalogReadRepository>());

    private sealed record Drink(EntityId Category, EntityId Item, EntityId Small, EntityId Large, EntityId Addon, EntityId Beans, EntityId Milk);
}

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable)))
        {
            Skip = $"Set {CatalogPersistenceIntegrationTests.ConnectionVariable} to run PostgreSQL integration tests.";
        }
    }
}

public sealed class PostgresTheoryAttribute : TheoryAttribute
{
    public PostgresTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable)))
        {
            Skip = $"Set {CatalogPersistenceIntegrationTests.ConnectionVariable} to run PostgreSQL integration tests.";
        }
    }
}
