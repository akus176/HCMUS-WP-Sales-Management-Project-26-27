namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.WinUI;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

// One ordered acceptance path: a freshly migrated/seeded database, then every write
// through the actual WinUI composition root using the restricted runtime login.
public sealed class MidtermDemonstrationIntegrationTests(ITestOutputHelper output) : IAsyncLifetime
{
    private readonly string databaseName = $"coffeeshop_demo_{Guid.NewGuid():N}";
    private readonly string roleName = $"demo_{Guid.NewGuid():N}";
    private string? adminConnection;
    private ServiceProvider services = null!;

    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable(CatalogPersistenceIntegrationTests.ConnectionVariable);
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        await ExecuteAsync(adminConnection, $"CREATE DATABASE \"{databaseName}\"");
        var builder = new NpgsqlConnectionStringBuilder(adminConnection) { Database = databaseName, Pooling = false };
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>();
        CoffeeShopPostgresOptions.Configure(options, builder.ConnectionString);
        await using (var db = new CoffeeShopDbContext(options.Options))
        {
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "midterm-seed.sql"));
            await db.Database.ExecuteSqlRawAsync(script);
        }
        var grants = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "midterm-role.sql"));
        await ExecuteAsync(builder.ConnectionString, grants.Replace("\"coffeeshop_midterm\"", $"\"{roleName}\"", StringComparison.Ordinal));
        var password = Guid.NewGuid().ToString("N");
        await ExecuteAsync(builder.ConnectionString, $"ALTER ROLE \"{roleName}\" PASSWORD '{password}'");
        builder.Username = roleName;
        builder.Password = password;
        services = new ServiceCollection().AddCoffeeShopDirectClient(builder.ConnectionString)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        output.WriteLine("PASS DEMO-01: new database; all migrations applied; seed loaded; runtime uses restricted login.");
    }

    public async Task DisposeAsync()
    {
        if (services is not null) await services.DisposeAsync();
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        await ExecuteAsync(adminConnection, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        await ExecuteAsync(adminConnection, $"DROP ROLE IF EXISTS \"{roleName}\"");
    }

    [PostgresFact]
    public async Task CompletePath_RebuiltDatabase_RestrictedClient_AllWorkflowsAndReports()
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<CoffeeShopDbContext>();
        Assert.True(await db.Database.CanConnectAsync());
        Assert.Equal(20, await db.MenuItems.CountAsync());
        Assert.Equal(5, await db.Categories.CountAsync());
        Assert.Equal(12, await db.Customers.CountAsync());
        Assert.Equal(24, await db.Orders.CountAsync());

        var pos = services.GetRequiredService<PosWorkspace>();
        await pos.LoadAsync();
        Assert.Null(pos.Error);
        pos.Session.Operator = pos.Options.Staff.Single(staff => staff.FullName == "Linh Tran");
        var actor = pos.Session.GetRequiredId();
        var operations = services.GetRequiredService<OperationsWorkspace>();
        operations.EnableOperationalWorkflow();

        var inventory = provider.GetRequiredService<InventoryCommandHandler>();
        var ingredient = Success(await inventory.CreateIngredientAsync(new("Demo acceptance beans", "g", ReorderThreshold: 30m)));
        var supplier = Success(await inventory.CreateSupplierAsync(new("Demo acceptance supplier", "0901234567")));
        Success(await inventory.UpdateSupplierAsync(new(supplier, "Demo updated supplier", "0901234567", null, "Demo Avenue")));
        var receipt = Success(await inventory.CreateDraftReceiptAsync(new(supplier, "DEMO-ACCEPTANCE-RECEIPT")));
        Success(await inventory.AddReceiptLineAsync(new(receipt, ingredient, 100m, 1m)));
        Success(await inventory.PostReceiptAsync(new(receipt, actor)));
        Assert.False((await inventory.PostReceiptAsync(new(receipt, actor))).IsSuccess);
        Assert.Equal(100m, await BalanceAsync(ingredient));
        var audit = services.GetRequiredService<InventoryAuditWorkspace>();
        await audit.SelectAsync(ingredient);
        Assert.Null(audit.Error);
        await audit.AdjustAsync(-5m, "");
        Assert.NotNull(audit.Error);
        Assert.Equal(100m, await BalanceAsync(ingredient));
        await audit.AdjustAsync(-5m, "Demo spillage");
        Assert.Null(audit.Error);
        await audit.AdjustAsync(5m, "Demo recount increase");
        Assert.Null(audit.Error);
        await audit.AdjustAsync(-5m, "Demo recount correction");
        Assert.Null(audit.Error);
        Assert.Equal(95m, audit.SelectedIngredient!.AvailableQuantity);
        Assert.Equal(4, audit.Movements.Count);
        Assert.Equal(95m, await BalanceAsync(ingredient));
        output.WriteLine("PASS DEMO-02: supplier created/updated; receipt posted once (+100g); blank adjustment rejected; -5/+5/-5g adjustments; balance 95g; four movements.");

        var catalog = provider.GetRequiredService<CatalogCommandHandler>();
        var category = Success(await catalog.CreateCategoryAsync(new("Demo acceptance coffee")));
        var itemId = Success(await catalog.CreateMenuItemAsync(new(category, "Demo acceptance drink", null, [new("Regular", 40m)])));
        Success(await catalog.UpdateMenuItemAsync(new(itemId, category, "Demo reviewed drink", "Acceptance run")));
        var variant = await db.SizeVariants.SingleAsync(size => size.MenuItemId == itemId);
        Success(await provider.GetRequiredService<RecipeCommandHandler>().ReplaceRecipeAsync(new(variant.Id, [new(ingredient, 10m, "g")])));
        var addon = Success(await catalog.CreateAddonAsync(new("Demo topping", 5m)));
        Success(await catalog.SetMenuItemAddonsAsync(new(itemId, [addon])));
        output.WriteLine("PASS DEMO-03: category/drink/variant/add-on created; drink updated; size recipe saved (10g).");

        var customers = services.GetRequiredService<CustomerWorkspace>();
        Assert.True(await customers.CreateAsync(new("Demo acceptance customer", "0901999103")));
        var customerId = customers.SelectedCustomer!.Id;
        Assert.True(await customers.UpdateAsync(new(customerId, "Demo reviewed customer", "0901999103")));
        Assert.True(await customers.AddAddressAsync(new(customerId, "103 Demo Avenue", "Home", true)));
        var addressId = customers.SelectedCustomer!.Addresses.Single().Id;
        Success(await provider.GetRequiredService<LoyaltyCommandHandler>().UpdateSettingsAsync(new(1m, 1m, 50m)));
        output.WriteLine("PASS DEMO-04: customer created/updated; saved default address created; loyalty rules configured.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var baselineRevenue = Success(await provider.GetRequiredService<RevenueReportQueryHandler>()
            .GetRevenueAsync(new(today, today, TimeZoneInfo.Utc))).Revenue;
        decimal completedTotal = 0m;
        foreach (var channel in new[] { "dine_in", "takeaway", "delivery" })
        {
            pos.NewTicket();
            pos.Channel = channel;
            await pos.LoadAsync();
            Assert.Null(pos.Error);
            pos.TableId = channel == "dine_in" ? pos.Options.Tables.First().Id : null;
            pos.Customer = channel == "takeaway" ? new() : new(customerId,
                IsDelivery: channel == "delivery", CustomerAddressId: channel == "delivery" ? addressId : null,
                RecipientName: "Demo reviewed customer", ContactNumber: "0901999103");
            var item = pos.Menu.Single(menu => menu.Id == itemId);
            pos.AddLine(item, item.SizeVariants.Single(), 1, [item.CompatibleAddons.Single()]);
            await pos.SaveDraftAsync();
            Assert.Null(pos.Error);
            Assert.Equal(45m, pos.Order!.Total);
            if (channel == "dine_in")
            {
                await pos.RequestDiscountAsync(DiscountType.Fixed, 5m, "Demo manager approval");
                Assert.Null(pos.Error);
                Assert.Equal(45m, pos.Order!.Total);
                await operations.LoadAsync();
                Assert.Null(operations.Error);
                operations.Select(operations.Orders.Single(order => order.Id == pos.TicketId));
                await operations.DecideDiscountAsync(pos.Order.Discounts.Single().Id, true);
                Assert.Null(operations.Error);
                await pos.LoadAsync();
                Assert.Equal(40m, pos.Order!.Total);
            }
            await pos.ConfirmAsync();
            Assert.Null(pos.Error);
            Assert.Equal(OrderStatus.Confirmed, pos.Order!.Status);
            await operations.LoadAsync();
            Assert.Null(operations.Error);
            operations.Select(operations.Orders.Single(order => order.Id == pos.TicketId));
            await operations.StartPreparingAsync();
            Assert.Null(operations.Error);
            await operations.MarkReadyAsync();
            Assert.Null(operations.Error);
            await operations.CompleteAsync();
            Assert.NotNull(operations.Error); // Unpaid orders must remain ready.
            var method = channel switch { "dine_in" => PaymentMethod.Cash, "takeaway" => PaymentMethod.Card, _ => PaymentMethod.EWallet };
            await operations.RecordPaymentAsync(method, pos.Order.Total, "DEMO-PAYMENT");
            Assert.Null(operations.Error);
            if (channel == "delivery")
            {
                await operations.AssignDeliveryAsync(Assert.Single(operations.DeliveryStaff).Id);
                Assert.Null(operations.Error);
                await operations.StartDeliveryAsync();
                Assert.Null(operations.Error);
                await operations.CompleteDeliveryAsync();
                Assert.Null(operations.Error);
                Assert.Equal(DeliveryStatus.Delivered, operations.SelectedOrder!.DeliveryStatus);
            }
            await operations.CompleteAsync();
            Assert.Null(operations.Error);
            Assert.Equal(OrderStatus.Completed, operations.SelectedOrder!.Status);
            await operations.CompleteAsync();
            Assert.NotNull(operations.Error); // Retry must not award points twice.
            if (channel == "dine_in")
                Assert.False(operations.Tables.Single(table => table.Id == pos.TableId).IsOccupied);
            completedTotal += pos.Order.Total;
            output.WriteLine($"PASS DEMO-05-{channel}: confirmed/preparing/ready/completed; unpaid completion rejected; {method} {pos.Order.Total:0.00}; completion retry rejected.");
        }
        await customers.RefreshAfterOrderCompletionAsync(customerId);
        Assert.Null(customers.Error);
        Assert.Equal(85, customers.SelectedCustomer!.LoyaltyPointsBalance);
        Assert.Equal(2, customers.History.Items.Count);
        output.WriteLine("PASS DEMO-06: two attached completed orders earned 40 + 45 = 85 points exactly once; walk-in earned none.");

        // Redeem on a new dine-in order, then cancel it after confirmation.
        pos.NewTicket();
        pos.Channel = "dine_in";
        await pos.LoadAsync();
        pos.TableId = pos.Options.Tables.First().Id;
        pos.Customer = new(customerId);
        var cancelItem = pos.Menu.Single(menu => menu.Id == itemId);
        pos.AddLine(cancelItem, cancelItem.SizeVariants.Single(), 1, []);
        await pos.SaveDraftAsync();
        Assert.Null(pos.Error);
        await pos.RedeemLoyaltyAsync(10);
        Assert.Null(pos.Error);
        Assert.Equal(30m, pos.Order!.Total);
        var before = await BalanceAsync(ingredient);
        await pos.ConfirmAsync();
        Assert.Null(pos.Error);
        Assert.Equal(before - 10m, await BalanceAsync(ingredient));
        await operations.LoadAsync();
        operations.Select(operations.Orders.Single(order => order.Id == pos.TicketId));
        await operations.CancelAsync("Demo cancellation restoration");
        Assert.Null(operations.Error);
        Assert.Equal(before, await BalanceAsync(ingredient));
        Assert.False(operations.Tables.Single(table => table.Id == pos.TableId).IsOccupied);
        await operations.CancelAsync("Repeat cancellation");
        Assert.NotNull(operations.Error);
        Assert.Equal(before, await BalanceAsync(ingredient));
        await customers.LoadAsync();
        Assert.Equal(85, customers.SelectedCustomer!.LoyaltyPointsBalance);
        Assert.Equal(4, customers.History.Items.Count);
        using (var verify = services.CreateScope())
        {
            var data = verify.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            Assert.Equal(1, await data.OrderInventoryRestorations.CountAsync(restoration => restoration.OrderId == pos.TicketId));
            Assert.Equal(2, await data.StockMovements.CountAsync(movement => movement.OrderId == pos.TicketId));
        }
        output.WriteLine("PASS DEMO-07: 10 points redeemed; confirm consumed 10g; cancel restored 10g, points and table once; repeat had no effects.");

        // Reports use the same scoped context sequentially, as the desktop page does.
        var revenue = Success(await provider.GetRequiredService<RevenueReportQueryHandler>().GetRevenueAsync(new(today, today, TimeZoneInfo.Utc)));
        var best = Success(await provider.GetRequiredService<BestSellerReportQueryHandler>().GetBestSellersAsync(new(today, today, TimeZoneInfo.Utc)));
        var channels = Success(await provider.GetRequiredService<OrderChannelReportQueryHandler>().GetChannelsAsync(new(today, today, TimeZoneInfo.Utc)));
        var ingredients = await provider.GetRequiredService<IInventoryRepository>().ListIngredientsAsync();
        Assert.Equal(130m, completedTotal);
        Assert.Equal(baselineRevenue + completedTotal, revenue.Revenue);
        Assert.Equal(revenue.Revenue, channels.ByChannel.Sum(channel => channel.Revenue));
        Assert.Equal(3L, best.Items.Where(row => row.MenuItemId == itemId).Sum(row => row.Quantity));
        Assert.NotEmpty(LowStockReport.Build(ingredients));
        Assert.Equal(65m, await BalanceAsync(ingredient));
        await audit.LoadAsync();
        Assert.Null(audit.Error);
        Assert.Equal(65m, audit.SelectedIngredient!.AvailableQuantity);
        Assert.Equal(9, audit.Movements.Count);
        Assert.Contains(audit.Movements, movement => movement.Type == StockMovementType.Restoration && movement.OrderId == pos.TicketId);
        Success(await catalog.DeactivateMenuItemAsync(new(itemId)));
        await pos.LoadAsync();
        Assert.DoesNotContain(pos.Menu, item => item.Id == itemId);
        output.WriteLine("PASS DEMO-08: revenue delta 130; channels reconcile; best seller quantity 3; cancellation excluded; low stock present; stock 65g; nine immutable movements; deactivated drink excluded from POS.");
    }

    [PostgresFact]
    public async Task FailedLoyaltyEarning_RollsBackCompletionAndTableRelease_ThenRecoversOnce()
    {
        var pos = services.GetRequiredService<PosWorkspace>();
        await pos.LoadAsync();
        Assert.Null(pos.Error);
        pos.Session.Operator = pos.Options.Staff.First();
        using var scope = services.CreateScope();
        var customer = Success(await scope.ServiceProvider.GetRequiredService<CustomerCommandHandler>()
            .CreateCustomerAsync(new("Completion rollback customer", "0901999104")));
        pos.Channel = "dine_in";
        pos.TableId = pos.Options.Tables.First().Id;
        pos.Customer = new(customer);
        var item = pos.Menu.Single(menu => menu.Name == "Almond Croissant");
        pos.AddLine(item, item.SizeVariants.Single(size => size.SizeName == "Regular"), 1, []);
        await pos.SaveDraftAsync();
        Assert.Null(pos.Error);
        await pos.ConfirmAsync();
        Assert.Null(pos.Error);
        var operations = services.GetRequiredService<OperationsWorkspace>();
        operations.EnableOperationalWorkflow();
        await operations.LoadAsync();
        Assert.Null(operations.Error);
        operations.Select(operations.Orders.Single(order => order.Id == pos.TicketId));
        await operations.StartPreparingAsync();
        Assert.Null(operations.Error);
        await operations.MarkReadyAsync();
        Assert.Null(operations.Error);
        await operations.RecordPaymentAsync(PaymentMethod.Cash, pos.Order!.Total, "");
        Assert.Null(operations.Error);
        var owner = new NpgsqlConnectionStringBuilder(adminConnection!) { Database = databaseName };
        await ExecuteAsync(owner.ConnectionString, "DELETE FROM customers.loyalty_settings");

        await operations.CompleteAsync();

        Assert.Equal("loyalty.settings_not_found", operations.Error?.Code);
        await operations.LoadAsync();
        Assert.Equal(OrderStatus.Ready, operations.SelectedOrder!.Status);
        Assert.True(operations.Tables.Single(table => table.Id == pos.TableId).IsOccupied);
        using (var verify = services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            Assert.Equal(0, await db.LoyaltyTransactions.CountAsync(entry => entry.OrderId == pos.TicketId));
            Assert.Equal(0, await db.OrderStatusHistories.CountAsync(entry => entry.OrderId == pos.TicketId && entry.ToStatus == OrderStatus.Completed));
        }
        Success(await scope.ServiceProvider.GetRequiredService<LoyaltyCommandHandler>().UpdateSettingsAsync(new(1m, 1m, 50m)));
        await operations.CompleteAsync();
        Assert.Null(operations.Error);
        Assert.False(operations.Tables.Single(table => table.Id == pos.TableId).IsOccupied);
        await operations.CompleteAsync();
        Assert.NotNull(operations.Error);
        using (var verify = services.CreateScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            Assert.Equal(1, await db.LoyaltyTransactions.CountAsync(entry => entry.OrderId == pos.TicketId));
        }
        output.WriteLine("PASS DEMO-09: missing loyalty settings rolled back completion/history/table release; configuration restored; retry completed and earned once.");
    }

    [PostgresFact]
    public async Task BoardActions_TargetTheRequestedNote_AndRejectSkippingOrUnpaidCompletion()
    {
        var pos = services.GetRequiredService<PosWorkspace>();
        await pos.LoadAsync();
        Assert.Null(pos.Error);
        pos.Session.Operator = pos.Options.Staff.First();
        pos.Channel = "takeaway";
        var item = pos.Menu.Single(menu => menu.Name == "Almond Croissant");
        pos.AddLine(item, item.SizeVariants.Single(size => size.SizeName == "Regular"), 1, []);
        await pos.SaveDraftAsync();
        Assert.Null(pos.Error);
        await pos.ConfirmAsync();
        Assert.Null(pos.Error);
        var firstId = pos.TicketId;

        pos.NewTicket();
        pos.AddLine(item, item.SizeVariants.Single(size => size.SizeName == "Regular"), 1, []);
        await pos.SaveDraftAsync();
        Assert.Null(pos.Error);
        await pos.ConfirmAsync();
        Assert.Null(pos.Error);
        var secondId = pos.TicketId;

        var board = services.GetRequiredService<OperationsWorkspace>();
        board.EnableOperationalWorkflow();
        await board.LoadAsync();
        Assert.Null(board.Error);
        board.Select(board.Orders.Single(order => order.Id == firstId));
        await board.MoveOrderAsync(secondId, OrderStatus.Ready);
        Assert.NotNull(board.Error);
        Assert.Equal(OrderStatus.Confirmed, board.Orders.Single(order => order.Id == secondId).Status);
        await board.MoveOrderAsync(secondId, OrderStatus.Preparing);
        Assert.Null(board.Error);
        Assert.Equal(secondId, board.SelectedOrderId);
        Assert.Equal(OrderStatus.Preparing, board.Orders.Single(order => order.Id == secondId).Status);
        Assert.Equal(OrderStatus.Confirmed, board.Orders.Single(order => order.Id == firstId).Status);

        await board.MoveOrderAsync(secondId, OrderStatus.Ready);
        Assert.Null(board.Error);
        await board.MoveOrderAsync(secondId, OrderStatus.Completed);
        Assert.NotNull(board.Error);
        Assert.Equal(OrderStatus.Ready, board.Orders.Single(order => order.Id == secondId).Status);
        await board.RecordPaymentAsync(PaymentMethod.Cash, pos.Order!.Total, "BOARD-TEST");
        Assert.Null(board.Error);
        await board.MoveOrderAsync(secondId, OrderStatus.Completed);
        Assert.Null(board.Error);
        Assert.Equal(OrderStatus.Completed, board.Orders.Single(order => order.Id == secondId).Status);
        await board.MoveOrderAsync(secondId, OrderStatus.Preparing);
        Assert.NotNull(board.Error);
    }

    private async Task<decimal> BalanceAsync(EntityId ingredient)
    {
        using var scope = services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IInventoryRepository>().FindIngredientAsync(ingredient))!.AvailableQuantity;
    }

    private static void Success(Result result) => Assert.True(result.IsSuccess, result.Error?.ToString());
    private static T Success<T>(Result<T> result) where T : notnull
    {
        Success((Result)result);
        return result.Value;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
