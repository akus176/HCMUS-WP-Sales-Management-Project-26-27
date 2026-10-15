namespace CoffeeShop.Application.Tests.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;

public sealed class OrderConfirmationCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Confirm_SufficientStock_DeductsIngredientsAndTransitionsOrder()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 2);
        var order = setup.Repository.Orders.Single().Value;

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.NotNull(order.ConfirmedAt);
        var ingredient = setup.Repository.Ingredients.Single().Value;
        Assert.Equal(64m, ingredient.AvailableQuantity); // 100 - 18 * 2
        var movement = Assert.Single(setup.Repository.StockMovements);
        Assert.Equal(StockMovementType.OrderConsumption, movement.Type);
        Assert.Equal(-36m, movement.QuantityDelta);
        Assert.Equal(order.Id, movement.OrderId);
        Assert.Equal(1, setup.Transactions.ExecutionCount);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Confirm_AggregatesSameIngredientAcrossLines()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 2, additionalLineQuantity: 1);
        var order = setup.Repository.Orders.Single().Value;

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsSuccess);
        var ingredient = setup.Repository.Ingredients.Single().Value;
        Assert.Equal(46m, ingredient.AvailableQuantity); // 100 - 18 * (2 + 1)
        var movement = Assert.Single(setup.Repository.StockMovements);
        Assert.Equal(-54m, movement.QuantityDelta);
    }

    [Fact]
    public async Task Confirm_InsufficientStock_ReturnsBusinessRuleWithoutAnyDeduction()
    {
        var setup = Setup(ingredientStock: 30m, recipeQuantity: 18m, lineQuantity: 2);
        var order = setup.Repository.Orders.Single().Value;

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.BusinessRule, result.Error!.Type);
        Assert.Equal("orders.insufficient_stock", result.Error.Code);
        Assert.Contains(result.Error.Details, detail => detail.Message.Contains("Coffee beans"));
        var ingredient = setup.Repository.Ingredients.Single().Value;
        Assert.Equal(30m, ingredient.AvailableQuantity);
        Assert.Empty(setup.Repository.StockMovements);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Confirm_MultiIngredientShortage_IdentifiesEveryShortage()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 2);
        var secondIngredient = new Ingredient(
            EntityId.New(), "Milk", "ml", Timestamp, Timestamp, availableQuantity: 10m);
        setup.Repository.Ingredients.Add(secondIngredient.Id, secondIngredient);
        var order = setup.Repository.Orders.Single().Value;
        var variant = setup.Repository.SizeVariants.Single().Value;
        var secondLine = new RecipeIngredient(EntityId.New(), variant, secondIngredient, 25m, "ml");
        setup.Repository.Recipes[variant.Id] = [.. setup.Repository.Recipes[variant.Id], secondLine];

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.BusinessRule, result.Error!.Type);
        Assert.Contains(result.Error.Details, detail => detail.Message.Contains("Milk"));
        Assert.DoesNotContain(result.Error.Details, detail => detail.Message.Contains("Coffee beans"));
        Assert.Equal(10m, secondIngredient.AvailableQuantity);
        Assert.Equal(100m, setup.Repository.Ingredients.Values.First(i => i.BaseUnit == "g").AvailableQuantity);
    }

    [Fact]
    public async Task Confirm_ConvertsRecipeUnitToIngredientBaseUnit()
    {
        var setup = Setup(ingredientStock: 20_000m, recipeQuantity: 18m, lineQuantity: 1, recipeUnit: "kg", ingredientUnit: "g");
        var order = setup.Repository.Orders.Single().Value;

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsSuccess);
        Assert.Equal(2_000m, setup.Repository.Ingredients.Single().Value.AvailableQuantity); // 20_000 - 18 kg = 18_000 g
        Assert.Equal(-18_000m, Assert.Single(setup.Repository.StockMovements).QuantityDelta);
    }

    [Fact]
    public async Task Confirm_InactiveIngredient_ReturnsBusinessRuleWithoutConfirmation()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1);
        var order = setup.Repository.Orders.Single().Value;
        // The recipe was created while active; deactivation must be revalidated at confirmation.
        setup.Repository.Ingredients.Single().Value.Deactivate(Timestamp);

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.BusinessRule, result.Error!.Type);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Empty(setup.Repository.StockMovements);
    }

    [Fact]
    public async Task Confirm_InactiveMenuItem_ReturnsValidationBeforeStockChecks()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1);
        var order = setup.Repository.Orders.Single().Value;
        setup.Repository.MenuItems.Single().Value.Deactivate(Timestamp);

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal("orders.catalog_unavailable", result.Error.Code);
        Assert.Equal(0, setup.Repository.IngredientLockCount);
        Assert.Equal(100m, setup.Repository.Ingredients.Single().Value.AvailableQuantity);
    }

    [Fact]
    public async Task Confirm_InactiveSizeVariant_ReturnsValidation()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1);
        var order = setup.Repository.Orders.Single().Value;
        setup.Repository.SizeVariants.Single().Value.Deactivate();

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal("orders.catalog_unavailable", result.Error!.Code);
    }

    [Fact]
    public async Task Confirm_InactiveAddon_ReturnsValidation()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1, withAddon: true);
        var order = setup.Repository.Orders.Single().Value;
        setup.Repository.Addons.Single().Value.Deactivate();

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal("orders.catalog_unavailable", result.Error!.Code);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Confirm_DineInOrder_OccupiesTheTable()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1, channel: "dine_in");
        var order = setup.Repository.Orders.Single().Value;
        var table = new DiningTable(EntityId.New(), "T1");
        setup.Repository.Tables[table.Id] = table;
        SetTableId(order, table.Id);

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsSuccess);
        Assert.True(table.IsOccupied);
        Assert.Equal(order.Id, table.ActiveOrderId);
        Assert.Equal(1, setup.Repository.TableLockCount);
    }

    [Fact]
    public async Task Confirm_OccupiedTable_ReturnsConflictAndKeepsOrderDraft()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1, channel: "dine_in");
        var order = setup.Repository.Orders.Single().Value;
        var otherOrderId = EntityId.New();
        var table = new DiningTable(EntityId.New(), "T1", activeOrderId: null);
        table.Occupy(otherOrderId, partySize: 1);
        setup.Repository.Tables[table.Id] = table;
        // Point the order at this table.
        SetTableId(order, table.Id);

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("orders.table_unavailable", result.Error.Code);
        Assert.Equal(OrderStatus.Draft, order.Status);
        Assert.Empty(setup.Repository.StockMovements);
    }

    [Fact]
    public async Task Confirm_AlreadyConfirmedOrder_ReturnsIdempotentSuccessWithoutNewDeduction()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1);
        var order = setup.Repository.Orders.Single().Value;
        Assert.True((await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()))).IsSuccess);
        var balanceAfterFirst = setup.Repository.Ingredients.Single().Value.AvailableQuantity;

        var retry = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(retry.IsSuccess);
        Assert.Equal(balanceAfterFirst, setup.Repository.Ingredients.Single().Value.AvailableQuantity);
        Assert.Single(setup.Repository.StockMovements);
    }

    [Fact]
    public async Task Confirm_CompletedOrder_ReturnsConflict()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1);
        var order = setup.Repository.Orders.Single().Value;
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, OrderStatus.Completed);

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal("orders.invalid_state", result.Error.Code);
    }

    [Fact]
    public async Task Confirm_MissingOrder_ReturnsNotFound()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1);

        var result = await setup.Handler.ConfirmAsync(new(EntityId.New(), EntityId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal("orders.order_not_found", result.Error.Code);
        Assert.Equal(1, setup.Transactions.RollbackCount);
    }

    [Fact]
    public async Task Confirm_NoRecipeIngredients_ConfirmWithoutStockOperations()
    {
        var setup = Setup(ingredientStock: 100m, recipeQuantity: 18m, lineQuantity: 1, withRecipe: false);
        var order = setup.Repository.Orders.Single().Value;

        var result = await setup.Handler.ConfirmAsync(new(order.Id, EntityId.New()));

        Assert.True(result.IsSuccess);
        Assert.Empty(setup.Repository.StockMovements);
        Assert.Equal(100m, setup.Repository.Ingredients.Single().Value.AvailableQuantity);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    private static UtcTimestamp Timestamp { get; } = new(Now);

    private static SetupState Setup(
        decimal ingredientStock,
        decimal recipeQuantity,
        int lineQuantity,
        string channel = "takeaway",
        string recipeUnit = "g",
        string ingredientUnit = "g",
        bool ingredientActive = true,
        bool withAddon = false,
        bool withRecipe = true,
        int additionalLineQuantity = 0)
    {
        var repository = new FakeOrderConfirmationRepository();
        var category = new Category(EntityId.New(), "Coffee", Timestamp, Timestamp);
        var item = new MenuItem(EntityId.New(), category, "Latte", Timestamp, Timestamp);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45_000m));
        var ingredient = new Ingredient(
            EntityId.New(), "Coffee beans", ingredientUnit, Timestamp, Timestamp,
            availableQuantity: ingredientStock, isActive: ingredientActive);

        repository.Categories[category.Id] = category;
        repository.MenuItems[item.Id] = item;
        repository.SizeVariants[variant.Id] = variant;
        repository.Ingredients[ingredient.Id] = ingredient;

        var selections = new List<OrderLineSelection> { new(item, variant, lineQuantity, [], []) };
        if (additionalLineQuantity > 0)
        {
            selections.Add(new OrderLineSelection(item, variant, additionalLineQuantity, [], []));
        }

        if (withAddon)
        {
            var addon = new Addon(EntityId.New(), "Extra shot", new Money(10_000m));
            repository.Addons[addon.Id] = addon;
            repository.CompatibilityLinks.Add(new MenuItemAddon(item.Id, addon.Id));
            selections[0] = new OrderLineSelection(item, variant, lineQuantity, [addon], [.. repository.CompatibilityLinks]);
        }

        var orderId = EntityId.New();
        var delivery = channel == "delivery"
            ? new Delivery(EntityId.New(), orderId, "Ada", "0900000000", "1 Main Street")
            : null;
        var order = new Order(
            orderId, $"ORD-{orderId.Value:N}"[..12], channel, EntityId.New(), Timestamp,
            selections.Select(selection => new OrderLine(
                EntityId.New(), selection.MenuItem, selection.SizeVariant, selection.Quantity,
                selection.Addons, selection.Compatibility)),
            tableId: channel == "dine_in" ? EntityId.New() : null,
            delivery: delivery);
        // dine_in requires a table id; assign the seeded table when present.
        if (channel == "dine_in" && repository.Tables.Count > 0)
        {
            SetTableId(order, repository.Tables.Keys.First());
        }
        repository.Orders[order.Id] = order;

        if (withRecipe)
        {
            repository.Recipes[variant.Id] = [new RecipeIngredient(EntityId.New(), variant, ingredient, recipeQuantity, recipeUnit)];
        }

        var unitOfWork = new FakeUnitOfWork();
        var transactions = new TestTransactionManager();
        var handler = new OrderConfirmationCommandHandler(
            repository, unitOfWork, transactions, new TestClock(Now));
        return new(handler, repository, unitOfWork, transactions, order);
    }

    private static void SetTableId(Order order, EntityId tableId)
    {
        typeof(Order).GetProperty(nameof(Order.TableId))!.SetValue(order, tableId);
    }

    private sealed record SetupState(
        OrderConfirmationCommandHandler Handler,
        FakeOrderConfirmationRepository Repository,
        FakeUnitOfWork UnitOfWork,
        TestTransactionManager Transactions,
        Order Order);
}
