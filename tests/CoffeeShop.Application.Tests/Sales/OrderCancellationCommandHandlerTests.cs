namespace CoffeeShop.Application.Tests.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Sales;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Sales;

public sealed class OrderCancellationCommandHandlerTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    public async Task Cancel_RecordsReasonActorAndTimeAndStopsOrderTransitions(string status)
    {
        var setup = Setup(status);
        var result = await setup.Handler.CancelAsync(new(setup.Order.Id, " Customer request "));

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, result.Value.Status);
        Assert.Equal(Now, result.Value.CancelledAt);
        Assert.Equal("Customer request", setup.Order.CancelReason);
        var history = Assert.Single(setup.Order.StatusHistory);
        Assert.Equal(setup.Actor.Id, history.ChangedBy);
        Assert.Equal(status, history.FromStatus);
        Assert.Throws<InvalidOperationException>(() => setup.Order.TransitionTo(OrderStatus.Preparing, setup.Actor.Id!, Now));
    }

    [Fact]
    public async Task Cancel_ReversesConsumptionAndBothLoyaltyTypesAndReleasesTableOnce()
    {
        var setup = Setup(OrderStatus.Ready, dineIn: true);
        var ingredient = new Ingredient(EntityId.New(), "Coffee", "g", Now, Now, availableQuantity: 6m);
        setup.Inventory.AddIngredient(ingredient);
        setup.Inventory.AddStockMovement(new(EntityId.New(), ingredient.Id, StockMovementType.OrderConsumption, -4m,
            setup.Actor.Id!, "Confirmed", Now, orderId: setup.Order.Id));
        var earned = setup.Customer.EarnLoyaltyPoints(EntityId.New(), 10, Now, setup.Order.Id);
        var redeemed = setup.Customer.RedeemLoyaltyPoints(EntityId.New(), 20, Now, setup.Order.Id);

        Assert.True((await setup.Handler.CancelAsync(new(setup.Order.Id, "Customer request"))).IsSuccess);
        var saved = setup.UnitOfWork.SaveChangesCallCount;
        var retry = await setup.Handler.CancelAsync(new(setup.Order.Id, "Different retry reason"));

        Assert.Equal(ErrorType.Conflict, retry.Error!.Type);
        Assert.Equal(saved, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Equal("Customer request", setup.Order.CancelReason);
        Assert.Equal(10m, ingredient.AvailableQuantity);
        Assert.Single(setup.Inventory.StockMovements, movement => movement.Type == StockMovementType.Restoration);
        Assert.Equal(100, setup.Customer.LoyaltyPointsBalance);
        Assert.Equal(4, setup.Customer.LoyaltyTransactions.Count);
        Assert.Single(setup.Customer.LoyaltyTransactions, transaction => transaction.ReversesTransactionId == earned.Id);
        Assert.Single(setup.Customer.LoyaltyTransactions, transaction => transaction.ReversesTransactionId == redeemed.Id);
        Assert.False(setup.Repository.Table!.IsOccupied);
        Assert.Null(setup.Repository.Table.ActiveOrderId);
        Assert.Single(setup.Order.StatusHistory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnauthorizedCancel_DoesNotLoadOrStartTransaction(bool authenticated)
    {
        var setup = Setup();
        var actor = authenticated ? TestCurrentUser.Authenticated(roles: ["Manager"]) : TestCurrentUser.Unauthenticated();
        var handler = CreateHandler(setup.Repository, setup.Inventory, setup.Customers,
            setup.UnitOfWork, setup.Transactions, actor);
        var result = await handler.CancelAsync(new(setup.Order.Id, "Reason"));

        Assert.Equal(authenticated ? ErrorType.Authorization : ErrorType.Authentication, result.Error!.Type);
        Assert.Equal(0, setup.Transactions.ExecutionCount);
        Assert.Equal(0, setup.Repository.LoadCount);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Cancel_MissingReason_LeavesOrderUnchanged(string? reason)
    {
        var setup = Setup();
        var result = await setup.Handler.CancelAsync(new(setup.Order.Id, reason!));
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(OrderStatus.Confirmed, setup.Order.Status);
        Assert.Equal(0, setup.Transactions.ExecutionCount);
    }

    [Fact]
    public async Task Cancel_OverlongReason_RejectsBeforeAnyEffect()
    {
        var setup = Setup();
        var result = await setup.Handler.CancelAsync(new(setup.Order.Id, new string('x', 256)));
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Empty(setup.Inventory.OrderRestorations);
    }

    [Fact]
    public async Task Cancel_CompletedOrder_DoesNotReverseOrSave()
    {
        var setup = Setup(OrderStatus.Completed);
        var result = await setup.Handler.CancelAsync(new(setup.Order.Id, "Reason"));
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Empty(setup.Order.StatusHistory);
    }

    [Fact]
    public async Task Cancel_MissingOrder_ReturnsNotFound()
    {
        var setup = Setup();
        var result = await setup.Handler.CancelAsync(new(EntityId.New(), "Reason"));
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Cancel_Draft_DoesNotReleaseAnotherOrdersTable()
    {
        var setup = Setup(OrderStatus.Draft, dineIn: true);
        var otherOrder = EntityId.New();
        setup.Repository.Table!.Occupy(otherOrder, 1);

        Assert.True((await setup.Handler.CancelAsync(new(setup.Order.Id, "Reason"))).IsSuccess);
        Assert.True(setup.Repository.Table.IsOccupied);
        Assert.Equal(otherOrder, setup.Repository.Table.ActiveOrderId);
    }

    private static SetupState Setup(string status = OrderStatus.Confirmed, bool dineIn = false)
    {
        var actor = TestCurrentUser.Authenticated(permissions: ["orders.cancel"]);
        var customer = new Customer(EntityId.New(), "Alice", "0901234567", Now, Now, loyaltyPointsBalance: 100);
        var table = dineIn ? new DiningTable(EntityId.New(), "T1") : null;
        var order = new StoredOrder(status, customer.Id, table?.Id);
        if (dineIn && status != OrderStatus.Draft) table!.Occupy(order.Id, 1);
        var repository = new CancellationRepository(order, table);
        var inventory = new FakeInventoryRepository();
        var customers = new FakeCustomerRepository();
        customers.Orders.Add(order.Id, order);
        customers.Customers.Add(customer.Id, customer);
        var unitOfWork = new FakeUnitOfWork();
        var transactions = new TestTransactionManager();
        return new(CreateHandler(repository, inventory, customers, unitOfWork, transactions, actor),
            repository, inventory, customers, unitOfWork, transactions, actor, order, customer);
    }

    private static OrderCancellationCommandHandler CreateHandler(CancellationRepository repository,
        FakeInventoryRepository inventory, FakeCustomerRepository customers, FakeUnitOfWork unitOfWork,
        TestTransactionManager transactions, TestCurrentUser actor)
    {
        var clock = new TestClock(Now.Value);
        return new(repository, new InventoryCommandHandler(inventory, unitOfWork, transactions, clock),
            new LoyaltyCommandHandler(customers, unitOfWork, transactions, clock, actor),
            unitOfWork, transactions, clock, actor);
    }

    private sealed class StoredOrder : Order
    {
        public StoredOrder(string status, EntityId customerId, EntityId? tableId)
        {
            Id = EntityId.New();
            Status = status;
            CustomerId = customerId;
            TableId = tableId;
            Channel = tableId is null ? "takeaway" : "dine_in";
        }
    }

    private sealed class CancellationRepository(Order order, DiningTable? table) : IOrderCancellationRepository
    {
        public int LoadCount { get; private set; }
        public DiningTable? Table { get; } = table;
        public Task<Order?> LoadOrderForCancellationAsync(EntityId orderId, CancellationToken cancellationToken = default)
        {
            LoadCount++;
            return Task.FromResult<Order?>(order.Id == orderId ? order : null);
        }

        public Task<DiningTable?> LoadTableForCancellationAsync(EntityId tableId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Table);
    }

    private sealed record SetupState(OrderCancellationCommandHandler Handler, CancellationRepository Repository,
        FakeInventoryRepository Inventory, FakeCustomerRepository Customers, FakeUnitOfWork UnitOfWork,
        TestTransactionManager Transactions, TestCurrentUser Actor, Order Order, Customer Customer);
}
