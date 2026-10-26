namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class MidtermSeedDataIntegrationTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_midterm_seed_{Guid.NewGuid():N}";
    private string adminConnectionString = null!;
    private ServiceProvider services = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable);
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
    public async Task MidtermSeed_IsCompleteRelatedAndRepeatableOnARebuiltDatabase()
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "midterm-seed.sql"));

        await context.Database.ExecuteSqlRawAsync(script);
        await AssertDatasetAsync(context);

        var snapshot = await context.Database.SqlQueryRaw<string>("""
            SELECT concat_ws('|',
                (SELECT count(*) FROM catalog.categories WHERE name LIKE 'Midterm %'),
                (SELECT count(*) FROM catalog.menu_items WHERE name LIKE 'Midterm %'),
                (SELECT count(*) FROM customers.customers WHERE phone LIKE '+8409008%'),
                (SELECT count(*) FROM sales.orders WHERE order_number LIKE 'MIDTERM-%'),
                (SELECT count(*) FROM inventory.stock_movements WHERE reason LIKE '%demo%')) AS "Value"
            """).SingleAsync();

        await context.Database.ExecuteSqlRawAsync(script);
        await AssertDatasetAsync(context);
        Assert.Equal(snapshot, await context.Database.SqlQueryRaw<string>("""
            SELECT concat_ws('|',
                (SELECT count(*) FROM catalog.categories WHERE name LIKE 'Midterm %'),
                (SELECT count(*) FROM catalog.menu_items WHERE name LIKE 'Midterm %'),
                (SELECT count(*) FROM customers.customers WHERE phone LIKE '+8409008%'),
                (SELECT count(*) FROM sales.orders WHERE order_number LIKE 'MIDTERM-%'),
                (SELECT count(*) FROM inventory.stock_movements WHERE reason LIKE '%demo%')) AS "Value"
            """).SingleAsync());
    }

    private static async Task AssertDatasetAsync(CoffeeShopDbContext context)
    {
        Assert.Equal(5, await CountAsync(context, "SELECT count(*) FROM catalog.categories WHERE name LIKE 'Midterm %'"));
        Assert.Equal(20, await CountAsync(context, "SELECT count(*) FROM catalog.menu_items WHERE name LIKE 'Midterm %'"));
        Assert.Equal(40, await CountAsync(context, "SELECT count(*) FROM catalog.size_variants sv JOIN catalog.menu_items mi ON mi.id = sv.menu_item_id WHERE mi.name LIKE 'Midterm %'"));
        Assert.Equal(40, await CountAsync(context, "SELECT count(*) FROM catalog.recipe_ingredients ri JOIN catalog.size_variants sv ON sv.id = ri.size_variant_id JOIN catalog.menu_items mi ON mi.id = sv.menu_item_id WHERE mi.name LIKE 'Midterm %'"));
        var ingredients = await context.Ingredients.AsNoTracking().ToDictionaryAsync(ingredient => ingredient.Id);
        foreach (var recipe in await context.RecipeIngredients.AsNoTracking().ToArrayAsync())
        {
            Assert.True(recipe.Quantity > 0);
            Assert.NotNull(ingredients[recipe.IngredientId].ConvertToBaseUnit(recipe.Quantity, recipe.Unit));
        }
        Assert.Equal(12, await CountAsync(context, "SELECT count(*) FROM customers.customers WHERE phone LIKE '+8409008%'"));
        Assert.Equal(24, await CountAsync(context, "SELECT count(*) FROM sales.orders WHERE order_number LIKE 'MIDTERM-%'"));
        Assert.Equal(1, await CountAsync(context, "SELECT count(*) FROM identity.users u JOIN identity.user_roles ur ON ur.user_id = u.id JOIN identity.roles r ON r.id = ur.role_id WHERE u.username = 'midterm_delivery' AND u.is_active AND r.code = 'DeliveryStaff'"));
        Assert.Equal(0, await CountAsync(context, "SELECT count(*) FROM sales.deliveries d JOIN sales.orders o ON o.id = d.order_id WHERE o.order_number LIKE 'MIDTERM-%' AND d.assigned_staff_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM identity.user_roles ur JOIN identity.roles r ON r.id = ur.role_id WHERE ur.user_id = d.assigned_staff_id AND r.code = 'DeliveryStaff')"));
        Assert.Equal(3, await CountAsync(context, "SELECT count(DISTINCT channel) FROM sales.orders WHERE order_number LIKE 'MIDTERM-%'"));
        Assert.Equal(8, await CountAsync(context, "SELECT count(*) FROM sales.tables WHERE table_number LIKE 'MIDTERM-%'"));
        Assert.Equal(3, await CountAsync(context, "SELECT count(*) FROM inventory.suppliers WHERE name LIKE 'Midterm %'"));
        Assert.Equal(2, await CountAsync(context, "SELECT count(*) FROM inventory.purchase_receipts WHERE receipt_number LIKE 'MIDTERM-%'"));
        Assert.True(await CountAsync(context, "SELECT count(*) FROM inventory.stock_movements WHERE actor_id = md5('coffeeshop-midterm-v1:staff-manager')::uuid") >= 3);
        Assert.True(await CountAsync(context, "SELECT count(*) FROM sales.payments p JOIN sales.orders o ON o.id = p.order_id WHERE o.order_number LIKE 'MIDTERM-%'") >= 20);
        Assert.True(await CountAsync(context, "SELECT count(*) FROM sales.discounts d JOIN sales.orders o ON o.id = d.order_id WHERE o.order_number LIKE 'MIDTERM-%'") >= 3);
        Assert.True(await CountAsync(context, "SELECT count(*) FROM sales.orders WHERE order_number LIKE 'MIDTERM-%' AND status = 'cancelled'") >= 2);
        Assert.Equal(5, await CountAsync(context, "SELECT count(DISTINCT d.status) FROM sales.deliveries d JOIN sales.orders o ON o.id = d.order_id WHERE o.order_number LIKE 'MIDTERM-%'"));
        Assert.Equal(3, await CountAsync(context, "SELECT count(DISTINCT method) FROM sales.payments p JOIN sales.orders o ON o.id = p.order_id WHERE o.order_number LIKE 'MIDTERM-%'"));
        Assert.Equal(2, await CountAsync(context, "SELECT count(DISTINCT type) FROM sales.discounts d JOIN sales.orders o ON o.id = d.order_id WHERE o.order_number LIKE 'MIDTERM-%'"));
        Assert.Equal(2, await CountAsync(context, "SELECT count(DISTINCT type) FROM customers.loyalty_transactions t JOIN sales.orders o ON o.id = t.order_id WHERE o.order_number LIKE 'MIDTERM-%'"));
        Assert.True(await CountAsync(context, "SELECT count(*) FROM inventory.ingredients WHERE name LIKE 'Midterm %' AND available_quantity <= reorder_threshold") >= 1);

        Assert.Equal(0, await CountAsync(context, """
            SELECT count(*) FROM sales.orders o WHERE o.order_number LIKE 'MIDTERM-%' AND
              ((o.channel = 'dine_in' AND o.table_id IS NULL) OR
               (o.channel = 'delivery' AND NOT EXISTS (SELECT 1 FROM sales.deliveries d WHERE d.order_id = o.id)))
            """));
        Assert.Equal(0, await CountAsync(context, """
            SELECT count(*) FROM sales.order_lines l JOIN sales.orders o ON o.id = l.order_id
            WHERE o.order_number LIKE 'MIDTERM-%' AND
              (l.menu_item_id IS NULL OR l.size_variant_id IS NULL OR NOT EXISTS
                (SELECT 1 FROM catalog.recipe_ingredients r WHERE r.size_variant_id = l.size_variant_id))
            """));
        Assert.Equal(0, await CountAsync(context, """
            SELECT count(*) FROM sales.orders o WHERE o.order_number LIKE 'MIDTERM-%' AND
              o.subtotal <> (SELECT COALESCE(sum(l.line_total), 0) FROM sales.order_lines l WHERE l.order_id = o.id)
            """));
        Assert.Equal(0, await CountAsync(context, """
            SELECT count(*) FROM sales.orders o WHERE o.order_number LIKE 'MIDTERM-%' AND o.status = 'completed'
              AND o.total <> (SELECT COALESCE(sum(p.amount), 0) FROM sales.payments p WHERE p.order_id = o.id AND p.status = 'succeeded')
            """));
    }

    private static async Task<int> CountAsync(CoffeeShopDbContext context, string sql)
    {
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT (" + sql + ")::int";
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
