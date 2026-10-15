namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Security;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

public sealed class PosWorkspaceIntegrationTests : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_pos_{Guid.NewGuid():N}";
    private string? adminConnection;
    private ServiceProvider services = null!;
    private EntityId actorId = null!;
    private EntityId tableId = null!;
    private EntityId customerId = null!;
    private EntityId addressId = null!;
    private EntityId ingredientId = null!;
    private PosWorkspace Model => services.GetRequiredService<PosWorkspace>();

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await create.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName, Pooling = false };
        services = Register(new ServiceCollection().AddCoffeeShopPostgres(builder.ConnectionString)).BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await db.Database.MigrateAsync();
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        actorId = EntityId.New();
        tableId = EntityId.New();
        customerId = EntityId.New();
        addressId = EntityId.New();
        ingredientId = EntityId.New();
        var category = new Category(EntityId.New(), "Coffee", now, now);
        var item = new MenuItem(EntityId.New(), category, "Latte", now, now);
        var small = new SizeVariant(EntityId.New(), item.Id, "Small", new Money(25m));
        var large = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(35m));
        var addon = new Addon(EntityId.New(), "Cream", new Money(5m));
        var beans = new Ingredient(ingredientId, "Beans", "g", now, now, availableQuantity: 100m);
        db.Users.Add(new User(actorId, "cashier", "cashier@example.test", "test-only-hash", "Cashier", null, now, now));
        db.DiningTables.Add(new DiningTable(tableId, "T1", seats: 4));
        db.Customers.Add(new Customer(customerId, "Mai", "0901234567", now, now));
        db.CustomerAddresses.Add(new CustomerAddress(addressId, customerId, "10 Coffee Street", "Home"));
        db.Categories.Add(category);
        db.MenuItems.Add(item);
        db.SizeVariants.AddRange(small, large);
        db.Addons.Add(addon);
        db.MenuItemAddons.Add(new MenuItemAddon(item.Id, addon.Id));
        db.Ingredients.Add(beans);
        db.RecipeIngredients.AddRange(new RecipeIngredient(EntityId.New(), small, beans, 10m, "g"),
            new RecipeIngredient(EntityId.New(), large, beans, 15m, "g"));
        await db.SaveChangesAsync();
        await Model.LoadAsync();
        Assert.Null(Model.Error);
        Model.Session.Operator = Assert.Single(Model.Options.Staff);
    }

    private static IServiceCollection Register(IServiceCollection collection) => collection
        .AddSingleton<IClock, SystemClock>()
        .AddSingleton<PosOperatorSession>()
        .AddSingleton<ICurrentUser>(provider => provider.GetRequiredService<PosOperatorSession>())
        .AddSingleton<CustomerOrderContext>().AddSingleton<PosWorkspace>()
        .AddScoped<CatalogQueryHandler>().AddScoped<CustomerOrderSelectionHandler>()
        .AddScoped<DiscountCommandHandler>().AddScoped<OrderConfirmationCommandHandler>().AddScoped<PosOrderHandler>();

    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        await using var connection = new NpgsqlConnection(adminConnection);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    [PostgresTheory]
    [InlineData("dine_in")]
    [InlineData("takeaway")]
    [InlineData("delivery")]
    public async Task Operator_CreatesReviewsAndConfirmsEachChannel(string channel)
    {
        Model.Channel = channel;
        Model.TableId = channel == "dine_in" ? tableId : null;
        if (channel == "delivery") Model.Customer = new(RecipientName: "Walk-in recipient", ContactNumber: "0901234567", AddressLine: "20 Paper Lane");
        AddDrink(quantity: 2, large: true);
        await Model.SaveDraftAsync();
        Assert.Null(Model.Error);
        Assert.False(Model.CanEdit);
        Assert.True(Model.CanConfirm);
        var line = Assert.Single(Model.Order!.Lines);
        Assert.Equal("Latte", line.ItemName);
        Assert.Equal("Large", line.SizeName);
        Assert.Equal(35m, line.UnitPrice);
        Assert.Equal(5m, Assert.Single(line.Addons).UnitPrice);
        Assert.Equal(80m, Model.Order.Total);
        await Model.ConfirmAsync();
        Assert.Null(Model.Error);
        Assert.Equal(OrderStatus.Confirmed, Model.Order.Status);
        Assert.False(Model.CanConfirm);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(70m, (await db.Ingredients.SingleAsync()).AvailableQuantity);
        Assert.Single(await db.StockMovements.ToListAsync());
        Assert.Equal(channel == "dine_in", (await db.DiningTables.SingleAsync()).IsOccupied);
        Assert.Equal(channel == "delivery" ? 1 : 0, await db.Deliveries.CountAsync());
        if (channel == "delivery") Assert.Equal("20 Paper Lane", Model.Order.AddressLine);

        // Even a fresh presentation scope retries the same confirmed ticket safely.
        var id = Model.TicketId;
        await Model.OpenAsync(id);
        Assert.True((await scope.ServiceProvider.GetRequiredService<PosOrderHandler>().ConfirmAsync(id, Model.Order.Total)).IsSuccess);
        Assert.Equal(1, await db.StockMovements.CountAsync());
        Assert.Equal(1, await db.Orders.CountAsync());
    }

    [PostgresFact]
    public async Task SavedCustomerSelection_UsesCurrentOwnedAddress_AndSurvivesNavigation()
    {
        var context = services.GetRequiredService<CustomerOrderContext>();
        var request = new CustomerOrderSelectionRequest(customerId, true, addressId, "Mai", "0901234567");
        using (var scope = services.CreateScope())
        {
            var selected = await scope.ServiceProvider.GetRequiredService<CustomerOrderSelectionHandler>().PrepareAsync(request);
            context.Set(request, selected.Value);
        }

        Model.UseCustomerSelection();
        Assert.Equal("Mai", Model.CustomerLabel);
        Assert.Equal("10 Coffee Street", Model.Customer.AddressLine);
        Model.Channel = "delivery";
        AddDrink();
        await Model.LoadAsync();
        Assert.Single(Model.Cart);
        await Model.SaveDraftAsync();
        Assert.Null(Model.Error);
        Assert.Equal(customerId, Model.Order!.CustomerId);
        Assert.Equal(addressId, Model.Order.CustomerAddressId);
        Assert.Equal("10 Coffee Street", Model.Order.AddressLine);
        await Model.ConfirmAsync();
        Assert.Equal(OrderStatus.Confirmed, Model.Order.Status);
    }

    [PostgresFact]
    public async Task InvalidDelivery_KeepsInputsAndCreatesNothing()
    {
        Model.Channel = "delivery";
        Model.Customer = new(RecipientName: "Mai");
        AddDrink();
        await Model.SaveDraftAsync();
        Assert.Equal("customers.invalid_delivery", Model.Error!.Code);
        Assert.Single(Model.Cart);
        Assert.Equal("Mai", Model.Customer.RecipientName);
        Assert.True(Model.CanEdit);
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders.CountAsync());
    }

    [PostgresFact]
    public async Task MissingDineInTable_KeepsDraftUnsaved()
    {
        Model.Channel = "dine_in";
        AddDrink();
        await Model.SaveDraftAsync();
        Assert.NotNull(Model.Error);
        Assert.True(Model.CanEdit);
        Assert.Null(Model.Order);
    }

    [PostgresFact]
    public async Task Shortage_LeavesDraftStockAndMovementsUnchanged()
    {
        AddDrink(quantity: 11);
        await Model.SaveDraftAsync();
        await Model.ConfirmAsync();
        Assert.Equal("orders.insufficient_stock", Model.Error!.Code);
        Assert.Equal(OrderStatus.Draft, Model.Order!.Status);
        Assert.True(Model.CanConfirm);
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        Assert.Equal(100m, (await db.Ingredients.SingleAsync()).AvailableQuantity);
        Assert.Empty(await db.StockMovements.ToListAsync());
    }

    [PostgresFact]
    public async Task ConcurrentTableAssignment_ShowsConflict_AndReloadsCurrentState()
    {
        Model.Channel = "dine_in";
        Model.TableId = tableId;
        AddDrink();
        await Model.SaveDraftAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            var other = new Order(EntityId.New(), "OTHER", "takeaway", actorId, new UtcTimestamp(DateTimeOffset.UtcNow),
                [new OrderLine(EntityId.New(), await db.MenuItems.SingleAsync(), await db.SizeVariants.FirstAsync(), 1)]);
            db.Orders.Add(other);
            (await db.DiningTables.SingleAsync()).Occupy(other.Id, 1);
            await db.SaveChangesAsync();
        }

        await Model.ConfirmAsync();
        Assert.Equal("orders.table_unavailable", Model.Error!.Code);
        Assert.True(Model.RequiresReload);
        Assert.False(Model.CanConfirm);
        await Model.LoadAsync();
        Assert.Null(Model.Error);
        Assert.Empty(Model.Options.Tables);
        Assert.Equal(OrderStatus.Draft, Model.Order!.Status);
    }

    [PostgresFact]
    public async Task PendingDiscount_DoesNotReduceTotal_ApprovedDiscountRequiresFreshReview()
    {
        AddDrink();
        await Model.SaveDraftAsync();
        await Model.RequestDiscountAsync("fixed", 5m, "Manager promotion");
        Assert.Null(Model.Error);
        Assert.Equal(30m, Model.Order!.Total);
        var discount = Assert.Single(Model.Order.Discounts);
        Assert.Equal(DiscountStatus.Pending, discount.Status);
        Assert.Equal(0m, Model.Order.DiscountTotal);
        Assert.False(Model.Session.HasPermission("discounts.approve"));
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            var order = await db.Orders.Include(order => order.Discounts).SingleAsync();
            order.ApproveDiscount(discount.Id, actorId, new UtcTimestamp(DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        await Model.ConfirmAsync();
        Assert.Equal("pos.total_changed", Model.Error!.Code);
        await Model.LoadAsync();
        Assert.Equal(25m, Model.Order.Total);
        Assert.Equal(5m, Model.Order.DiscountTotal);
        Assert.Equal(DiscountStatus.Approved, Assert.Single(Model.Order.Discounts).Status);
        await Model.ConfirmAsync();
        Assert.Null(Model.Error);
        Assert.Equal(OrderStatus.Confirmed, Model.Order.Status);
    }

    [PostgresFact]
    public async Task StableTicketIdentity_DoesNotDuplicateAnUncertainSave()
    {
        AddDrink();
        var id = Model.TicketId;
        var command = new CreatePosDraftCommand(id, "takeaway", null, new(), Model.Cart.Select(line => line.Request).ToArray());
        using (var scope = services.CreateScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<PosOrderHandler>().CreateDraftAsync(command)).IsSuccess);
        }

        await Model.SaveDraftAsync();
        Assert.Null(Model.Error);
        Assert.Equal(id, Model.Order!.Id);
        using var verify = services.CreateScope();
        Assert.Equal(1, await verify.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders.CountAsync());
    }

    [PostgresFact]
    public async Task InactiveCustomerAfterSave_PreventsConfirmation()
    {
        Model.Customer = new(customerId);
        AddDrink();
        await Model.SaveDraftAsync();
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            (await db.Customers.SingleAsync()).Deactivate(new UtcTimestamp(DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        await Model.ConfirmAsync();
        Assert.Equal("customers.inactive_selection", Model.Error!.Code);
        Assert.Equal(OrderStatus.Draft, Model.Order!.Status);
    }

    [PostgresFact]
    public async Task NoOperator_IsValidation_NotAnAnonymousWrite()
    {
        Model.Session.Operator = null;
        AddDrink();
        await Model.SaveDraftAsync();
        Assert.Equal("pos.operator_required", Model.Error!.Code);
        Assert.True(Model.CanEdit);
        Assert.Null(Model.Order);
    }

    [PostgresFact]
    public async Task SearchAndNotFound_AreVisibleWithoutLosingCart()
    {
        AddDrink();
        Model.Search = "no matching drink";
        await Model.LoadAsync();
        Assert.True(Model.IsEmpty);
        Assert.Single(Model.Cart);
        await Model.OpenAsync(EntityId.New());
        Assert.Equal("pos.order_not_found", Model.Error!.Code);
        Assert.Single(Model.Cart);
    }

    [PostgresFact]
    public async Task ReloadErrorState_RecoversSavedTicketWithoutCreatingAnotherOrder()
    {
        AddDrink();
        await Model.SaveDraftAsync();
        var id = Model.TicketId;
        using var brokenServices = Register(new ServiceCollection()
            .AddCoffeeShopPostgres("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1;Pooling=false")).BuildServiceProvider();
        var broken = new PosWorkspace(brokenServices.GetRequiredService<IServiceScopeFactory>(), new(), new());
        await broken.LoadAsync();
        Assert.Equal("pos.database_error", broken.Error!.Code);
        Assert.True(broken.RequiresReload);
        Assert.False(broken.CanEdit);
        Assert.False(broken.CanConfirm);

        // Recreate the presentation with the healthy connection and reopen the saved identity.
        var reopened = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), Model.Session);
        await reopened.OpenAsync(id);
        Assert.Null(reopened.Error);
        Assert.Equal(id, reopened.Order!.Id);
        await reopened.ConfirmAsync();
        Assert.Equal(OrderStatus.Confirmed, reopened.Order.Status);
        using var scope = services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>().Orders.CountAsync());
    }

    [PostgresFact]
    public async Task SavedSnapshots_AreNotRecalculatedFromLaterCatalogEdits()
    {
        AddDrink();
        await Model.SaveDraftAsync();
        var before = Model.Order!;
        await Model.ConfirmAsync();
        Assert.Null(Model.Error);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            var small = await db.SizeVariants.SingleAsync(size => size.SizeName == "Small");
            small.Update("Renamed", new Money(50m));
            (await db.Addons.SingleAsync()).Update("New topping", new Money(10m));
            await db.SaveChangesAsync();
        }

        await Model.LoadAsync();
        Assert.Null(Model.Error);
        Assert.Equal(before.Total, Model.Order!.Total);
        var line = Assert.Single(Model.Order.Lines);
        Assert.Equal("Small", line.SizeName);
        Assert.Equal(25m, line.UnitPrice);
        Assert.Equal("Cream", Assert.Single(line.Addons).Name);
        Assert.Equal(5m, Assert.Single(line.Addons).UnitPrice);
    }

    [PostgresFact]
    public async Task Loading_DisablesEditsAndDuplicateOperations()
    {
        var observedBusy = false;
        var reentered = false;
        Model.PropertyChanged += (_, _) =>
        {
            if (!Model.IsBusy) return;
            observedBusy = true;
            Assert.False(Model.CanEdit);
            Assert.False(Model.CanConfirm);
            Assert.False(Model.IsEmpty);
            if (!reentered)
            {
                reentered = true;
                Assert.True(Model.SaveDraftAsync().IsCompletedSuccessfully);
            }
        };
        await Model.LoadAsync();
        Assert.True(observedBusy);
        Assert.True(reentered);
        Assert.False(Model.IsBusy);
        Assert.Null(Model.Order);
    }

    [PostgresFact]
    public async Task StaleCatalogSelection_IsRejectedOnSave()
    {
        AddDrink();
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            (await db.Categories.SingleAsync()).Deactivate(new UtcTimestamp(DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        await Model.SaveDraftAsync();
        Assert.Equal("pos.invalid_selection", Model.Error!.Code);
        Assert.Single(Model.Cart);
        Assert.Null(Model.Order);
        await Model.LoadAsync();
        Assert.True(Model.IsEmpty);
    }

    private void AddDrink(int quantity = 1, bool large = false)
    {
        var item = Assert.Single(Model.Menu);
        var size = item.SizeVariants.Single(size => size.SizeName == (large ? "Large" : "Small"));
        Model.AddLine(item, size, quantity, item.CompatibleAddons);
    }
}
