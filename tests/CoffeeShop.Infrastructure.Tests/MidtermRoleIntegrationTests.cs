namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class MidtermRoleIntegrationTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_role_{Guid.NewGuid():N}";
    private readonly string roleName = $"midterm_{Guid.NewGuid():N}";
    private readonly EntityId actorId = EntityId.New();
    private string? adminConnectionString;
    private string runtimeConnectionString = null!;
    private ServiceProvider services = null!;

    public async Task InitializeAsync()
    {
        adminConnectionString = Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(adminConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"CREATE DATABASE \"{databaseName}\"");
        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName, Pooling = false };
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>();
        CoffeeShopPostgresOptions.Configure(options, builder.ConnectionString);
        await using (var db = new CoffeeShopDbContext(options.Options))
        {
            await db.Database.MigrateAsync();
            var now = new UtcTimestamp(DateTimeOffset.UtcNow);
            db.Users.Add(new User(actorId, "midterm_actor", "midterm@test.local", "test-only-hash", "Operator", null, now, now));
            await db.SaveChangesAsync();
        }

        await using var owner = new NpgsqlConnection(builder.ConnectionString);
        await owner.OpenAsync();
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "midterm-role.sql"));
        await ExecuteAsync(owner, script.Replace("\"coffeeshop_midterm\"", $"\"{roleName}\"", StringComparison.Ordinal));
        var password = Guid.NewGuid().ToString("N");
        await using var setPassword = new NpgsqlCommand("SELECT format('ALTER ROLE %I PASSWORD %L', @role, @password)", owner);
        setPassword.Parameters.AddWithValue("role", roleName);
        setPassword.Parameters.AddWithValue("password", password);
        await ExecuteAsync(owner, (string)(await setPassword.ExecuteScalarAsync())!);
        builder.Username = roleName;
        builder.Password = password;
        runtimeConnectionString = builder.ConnectionString;
        services = new ServiceCollection().AddCoffeeShopPostgres(runtimeConnectionString)
            .AddSingleton<IClock, SystemClock>().AddSingleton<ICurrentUser>(new RuntimeActor(actorId))
            .AddScoped<CatalogCommandHandler>().AddScoped<RecipeCommandHandler>()
            .AddScoped<CustomerCommandHandler>().AddScoped<OrderConfirmationCommandHandler>()
            .AddScoped<PaymentCommandHandler>().BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        if (string.IsNullOrWhiteSpace(adminConnectionString)) return;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{databaseName}_denied\" WITH (FORCE)");
        await ExecuteAsync(admin, $"DROP ROLE IF EXISTS \"{roleName}_denied\"");
        await ExecuteAsync(admin, $"DROP ROLE IF EXISTS \"{roleName}\"");
    }

    [PostgresFact]
    public async Task RuntimeLogin_CreatesCatalogAndCustomer_ConfirmsPaysAndCancelsOrder()
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<CoffeeShopDbContext>();
        Assert.True(await db.Database.CanConnectAsync());
        var catalog = provider.GetRequiredService<CatalogCommandHandler>();
        var category = (await catalog.CreateCategoryAsync(new("Coffee"))).Value;
        var item = (await catalog.CreateMenuItemAsync(new(category, "Latte", null, [new("Cup", 25m)]))).Value;
        var variant = await db.SizeVariants.SingleAsync();
        var addon = (await catalog.CreateAddonAsync(new("Cream", 5m))).Value;
        Assert.True((await catalog.SetMenuItemAddonsAsync(new(item, [addon]))).IsSuccess);
        Assert.True((await catalog.SetMenuItemAddonsAsync(new(item, []))).IsSuccess);
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var ingredient = new Ingredient(EntityId.New(), "Beans", "g", now, now, availableQuantity: 100m);
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        var recipes = provider.GetRequiredService<RecipeCommandHandler>();
        Assert.True((await recipes.ReplaceRecipeAsync(new(variant.Id, [new(ingredient.Id, 10m, "g")]))).IsSuccess);
        Assert.True((await recipes.ReplaceRecipeAsync(new(variant.Id, [new(ingredient.Id, 20m, "g")]))).IsSuccess);
        var customer = await provider.GetRequiredService<CustomerCommandHandler>().CreateCustomerAsync(new("Mai", "0901234567"));
        Assert.True(customer.IsSuccess);
        Assert.Single((await provider.GetRequiredService<IPosRepository>().GetOptionsAsync()).Staff);
        Assert.False(await provider.GetRequiredService<IDeliveryRepository>().IsEligibleDeliveryStaffAsync(actorId));
        var order = new Order(EntityId.New(), "MIDTERM-1", "takeaway", actorId, now,
            [new OrderLine(EntityId.New(), await db.MenuItems.SingleAsync(), variant, 1)], customerId: customer.Value);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        Assert.True((await provider.GetRequiredService<OrderConfirmationCommandHandler>().ConfirmAsync(new(order.Id, actorId))).IsSuccess);
        Assert.Equal(80m, (await db.Ingredients.SingleAsync()).AvailableQuantity);
        Assert.True((await provider.GetRequiredService<PaymentCommandHandler>().RecordAsync(new(EntityId.New(), order.Id, PaymentMethod.Cash, 25m))).IsSuccess);
        Assert.True((await provider.GetRequiredService<OrderCancellationCommandHandler>().CancelAsync(new(order.Id, "Customer request"))).IsSuccess);
        Assert.Equal(100m, (await db.Ingredients.SingleAsync()).AvailableQuantity);
        Assert.Equal(2, await db.StockMovements.CountAsync());
        Assert.Single(await db.OrderInventoryRestorations.ToArrayAsync());
        Assert.Equal(2, await db.OrderStatusHistories.CountAsync());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var revenue = await provider.GetRequiredService<RevenueReportQueryHandler>().GetRevenueAsync(new(today, today, TimeZoneInfo.Utc));
        Assert.True(revenue.IsSuccess);
        Assert.Equal(0m, revenue.Value.Revenue);
    }

    [PostgresFact]
    public async Task RuntimeLogin_EditsAndPostsReceipt_AdjustsStockAndConfiguresLoyalty()
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var inventory = provider.GetRequiredService<InventoryCommandHandler>();
        var ingredient = (await inventory.CreateIngredientAsync(new("Beans", "g"))).Value;
        var supplier = (await inventory.CreateSupplierAsync(new("Supplier", "0901234567"))).Value;
        var receipt = (await inventory.CreateDraftReceiptAsync(new(supplier, "MIDTERM-RECEIPT"))).Value;
        var line = (await inventory.AddReceiptLineAsync(new(receipt, ingredient, 50m, 1m))).Value;
        Assert.True((await inventory.RemoveReceiptLineAsync(new(receipt, line))).IsSuccess);
        Assert.True((await inventory.AddReceiptLineAsync(new(receipt, ingredient, 100m, 1m))).IsSuccess);
        Assert.True((await inventory.PostReceiptAsync(new(receipt, actorId))).IsSuccess);
        Assert.True((await inventory.RecordStockAdjustmentAsync(new(ingredient, -10m, "Spillage", actorId))).IsSuccess);
        var db = provider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(90m, (await db.Ingredients.SingleAsync()).AvailableQuantity);
        Assert.Equal(2, await db.StockMovements.CountAsync());
        var loyalty = provider.GetRequiredService<LoyaltyCommandHandler>();
        Assert.True((await loyalty.UpdateSettingsAsync(new(1m, 1m, 50m))).IsSuccess);
        Assert.True((await loyalty.UpdateSettingsAsync(new(2m, 1m, 50m))).IsSuccess);
        Assert.Single(await db.LoyaltySettings.ToArrayAsync());
    }

    [PostgresFact]
    public async Task RuntimeLogin_HasNoAdministrativeAttributesMembershipsOrOwnership()
    {
        await using var connection = new NpgsqlConnection(runtimeConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT rolcanlogin AND NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls OR rolinherit)
                AND NOT EXISTS (SELECT 1 FROM pg_auth_members WHERE member = r.oid)
                AND NOT EXISTS (SELECT 1 FROM pg_database WHERE datdba = r.oid)
                AND NOT EXISTS (SELECT 1 FROM pg_namespace WHERE nspowner = r.oid)
                AND NOT EXISTS (SELECT 1 FROM pg_class WHERE relowner = r.oid)
                AND NOT has_table_privilege(current_user, 'catalog.categories', 'SELECT WITH GRANT OPTION')
            FROM pg_roles r WHERE rolname = current_user
            """, connection);
        Assert.Equal(true, await command.ExecuteScalarAsync());
    }

    [PostgresTheory]
    [InlineData("CREATE DATABASE \"{database}_denied\"")]
    [InlineData("CREATE ROLE \"{role}_denied\"")]
    [InlineData("SET ROLE pg_read_all_data")]
    [InlineData("ALTER ROLE \"{role}\" SUPERUSER")]
    [InlineData("CREATE SCHEMA forbidden")]
    [InlineData("CREATE TABLE public.forbidden (id integer)")]
    [InlineData("CREATE TABLE catalog.forbidden (id integer)")]
    [InlineData("CREATE TEMP TABLE forbidden (id integer)")]
    [InlineData("ALTER TABLE catalog.categories ADD COLUMN forbidden integer")]
    [InlineData("DROP TABLE catalog.recipe_ingredients")]
    [InlineData("TRUNCATE catalog.recipe_ingredients")]
    [InlineData("SELECT * FROM public.\"__EFMigrationsHistory\"")]
    [InlineData("SELECT password_hash FROM identity.users")]
    [InlineData("SELECT * FROM identity.refresh_tokens")]
    [InlineData("UPDATE identity.users SET is_active = false")]
    [InlineData("DELETE FROM catalog.categories")]
    [InlineData("UPDATE inventory.stock_movements SET reason = 'forbidden'")]
    [InlineData("DELETE FROM inventory.stock_movements")]
    [InlineData("UPDATE customers.loyalty_transactions SET points = 0")]
    [InlineData("DELETE FROM customers.loyalty_transactions")]
    [InlineData("DELETE FROM sales.order_status_history")]
    [InlineData("DELETE FROM sales.payments")]
    public async Task RuntimeLogin_RejectsUnneededOperations(string sql)
    {
        await using var connection = new NpgsqlConnection(runtimeConnectionString);
        await connection.OpenAsync();
        sql = sql.Replace("{database}", databaseName, StringComparison.Ordinal).Replace("{role}", roleName, StringComparison.Ordinal);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class RuntimeActor(EntityId id) : ICurrentUser
    {
        public EntityId Id => id;
        public string Username => "midterm_actor";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => [];
        public IReadOnlyCollection<string> Permissions => ["orders.cancel", "payments.record"];
        public bool HasPermission(string permission) => Permissions.Contains(permission);
        public bool IsInRole(string role) => false;
        public EntityId GetRequiredId() => id;
    }
}
