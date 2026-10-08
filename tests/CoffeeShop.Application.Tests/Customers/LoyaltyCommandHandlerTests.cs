namespace CoffeeShop.Application.Tests.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;

public sealed class LoyaltyCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EarnForCompletedOrder_PersistsOnceAndReturnsExistingOnRetry()
    {
        var setup = Setup(balance: 0, OrderStatus.Completed, total: 100m, paid: 100m);

        var first = await setup.Handler.EarnForCompletedOrderAsync(new(setup.Order.Id));
        var retry = await setup.Handler.EarnForCompletedOrderAsync(new(setup.Order.Id));

        Assert.True(first.IsSuccess);
        Assert.Equal(10, first.Value.Points);
        Assert.False(first.Value.AlreadyApplied);
        Assert.True(retry.Value.AlreadyApplied);
        Assert.Equal(first.Value.TransactionId, retry.Value.TransactionId);
        Assert.Equal(10, setup.Customer.LoyaltyPointsBalance);
        Assert.Single(setup.Customer.LoyaltyTransactions);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(2, setup.Transactions.ExecutionCount);
    }

    [Fact]
    public async Task RedeemForOrder_UpdatesOrderAndLedgerAtomicallyAndIsRetrySafe()
    {
        var setup = Setup(balance: 100, OrderStatus.Draft, total: 100m, paid: 0m);

        var first = await setup.Handler.RedeemForOrderAsync(new(setup.Order.Id, 20));
        var retry = await setup.Handler.RedeemForOrderAsync(new(setup.Order.Id, 20));

        Assert.True(first.IsSuccess);
        Assert.Equal(20m, first.Value.DiscountAmount);
        Assert.Equal(80m, setup.Order.Total.Amount);
        Assert.Equal(20, setup.Order.LoyaltyPointsRedeemed);
        Assert.Equal(80, setup.Customer.LoyaltyPointsBalance);
        Assert.Single(setup.Customer.LoyaltyTransactions);
        Assert.True(retry.Value.AlreadyApplied);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task RedeemRetry_WithDifferentPoints_ReturnsConflictWithoutChangingBalanceAgain()
    {
        var setup = Setup(balance: 100, OrderStatus.Draft, total: 100m, paid: 0m);
        Assert.True((await setup.Handler.RedeemForOrderAsync(new(setup.Order.Id, 20))).IsSuccess);

        var result = await setup.Handler.RedeemForOrderAsync(new(setup.Order.Id, 10));

        AssertError(result, ErrorType.Conflict, "loyalty.retry_mismatch");
        Assert.Equal(80, setup.Customer.LoyaltyPointsBalance);
        Assert.Single(setup.Customer.LoyaltyTransactions);
    }

    [Fact]
    public async Task ReverseCancelledOrder_ReversesEarnAndRedeemOnce()
    {
        var setup = Setup(balance: 0, OrderStatus.Cancelled, total: 100m, paid: 100m);
        var earned = setup.Customer.EarnLoyaltyPoints(
            EntityId.New(), 50, Timestamp(), setup.Order.Id, setup.Actor.Id);
        var redeemed = setup.Customer.RedeemLoyaltyPoints(
            EntityId.New(), 20, Timestamp(1), setup.Order.Id, setup.Actor.Id);

        var first = await setup.Handler.ReverseForCancelledOrderAsync(new(setup.Order.Id));
        var retry = await setup.Handler.ReverseForCancelledOrderAsync(new(setup.Order.Id));

        Assert.True(first.IsSuccess);
        Assert.Equal(2, first.Value.TransactionIds.Count);
        Assert.False(first.Value.AlreadyApplied);
        Assert.True(retry.Value.AlreadyApplied);
        Assert.Equal(0, setup.Customer.LoyaltyPointsBalance);
        Assert.Equal(4, setup.Customer.LoyaltyTransactions.Count);
        Assert.Contains(setup.Customer.LoyaltyTransactions, item => item.ReversesTransactionId == earned.Id);
        Assert.Contains(setup.Customer.LoyaltyTransactions, item => item.ReversesTransactionId == redeemed.Id);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ReverseEarn_WhenPointsWereSpentElsewhere_ReturnsConflictWithoutMutation()
    {
        var setup = Setup(balance: 0, OrderStatus.Cancelled, total: 100m, paid: 100m);
        setup.Customer.EarnLoyaltyPoints(EntityId.New(), 20, Timestamp(), setup.Order.Id, setup.Actor.Id);
        setup.Customer.RedeemLoyaltyPoints(EntityId.New(), 20, Timestamp(1), EntityId.New(), setup.Actor.Id);
        var count = setup.Customer.LoyaltyTransactions.Count;

        var result = await setup.Handler.ReverseForCancelledOrderAsync(new(setup.Order.Id));

        AssertError(result, ErrorType.Conflict, "loyalty.reversal_balance_conflict");
        Assert.Equal(0, setup.Customer.LoyaltyPointsBalance);
        Assert.Equal(count, setup.Customer.LoyaltyTransactions.Count);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateSettings_CreatesThenUpdatesSingleton()
    {
        var repository = new FakeCustomerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var transactions = new TestTransactionManager();
        var actor = TestCurrentUser.Authenticated();
        var handler = new LoyaltyCommandHandler(
            repository, unitOfWork, transactions, new TestClock(Now), actor);

        var created = await handler.UpdateSettingsAsync(new(0.1m, 1m, 20m));
        var updated = await handler.UpdateSettingsAsync(new(0.2m, 2m, 30m));

        Assert.Equal(created.Value, updated.Value);
        Assert.Equal(0.2m, repository.Settings!.EarnPointsPerCurrencyUnit);
        Assert.Equal(2m, repository.Settings.RedeemValuePerPoint);
        Assert.Equal(2, unitOfWork.SaveChangesCallCount);
        Assert.Equal(2, repository.SettingsLockCount);
    }

    [Fact]
    public async Task UnauthenticatedActor_ReturnsAuthenticationWithoutOpeningTransaction()
    {
        var repository = new FakeCustomerRepository();
        var transactions = new TestTransactionManager();
        var handler = new LoyaltyCommandHandler(
            repository,
            new FakeUnitOfWork(),
            transactions,
            new TestClock(Now),
            TestCurrentUser.Unauthenticated());

        var result = await handler.EarnForCompletedOrderAsync(new(EntityId.New()));

        AssertError(result, ErrorType.Authentication, "loyalty.authentication_required");
        Assert.Equal(0, transactions.ExecutionCount);
    }

    [Fact]
    public async Task MissingOrderAndSettings_ReturnSharedNotFoundResults()
    {
        var repository = new FakeCustomerRepository();
        var handler = new LoyaltyCommandHandler(
            repository,
            new FakeUnitOfWork(),
            new TestTransactionManager(),
            new TestClock(Now),
            TestCurrentUser.Authenticated());

        var missingOrder = await handler.EarnForCompletedOrderAsync(new(EntityId.New()));
        AssertError(missingOrder, ErrorType.NotFound, "loyalty.order_not_found");

        var customer = Customer(0);
        var order = new StoredOrder(EntityId.New(), customer.Id, OrderStatus.Completed, 100m, 100m);
        repository.Customers.Add(customer.Id, customer);
        repository.Orders.Add(order.Id, order);
        var missingSettings = await handler.EarnForCompletedOrderAsync(new(order.Id));
        AssertError(missingSettings, ErrorType.NotFound, "loyalty.settings_not_found");
    }

    private static SetupState Setup(int balance, string status, decimal total, decimal paid)
    {
        var repository = new FakeCustomerRepository();
        var customer = Customer(balance);
        var order = new StoredOrder(EntityId.New(), customer.Id, status, total, paid);
        repository.Customers.Add(customer.Id, customer);
        repository.Orders.Add(order.Id, order);
        repository.Settings = new LoyaltySettings(EntityId.New(), 0.1m, 1m, 20m, Timestamp());
        var unitOfWork = new FakeUnitOfWork();
        var transactions = new TestTransactionManager();
        var actor = TestCurrentUser.Authenticated();
        var handler = new LoyaltyCommandHandler(
            repository, unitOfWork, transactions, new TestClock(Now), actor);
        return new(handler, repository, unitOfWork, transactions, actor, customer, order);
    }

    private static Customer Customer(int balance) => new(
        EntityId.New(), "Alice", "0901234567", Timestamp(), Timestamp(), loyaltyPointsBalance: balance);

    private static UtcTimestamp Timestamp(int minutes = 0) => new(Now.AddMinutes(minutes));

    private static void AssertError(Result result, ErrorType type, string code)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(type, result.Error!.Type);
        Assert.Equal(code, result.Error.Code);
    }

    private sealed class StoredOrder : Order
    {
        public StoredOrder(EntityId id, EntityId customerId, string status, decimal total, decimal paid)
        {
            Id = id;
            CustomerId = customerId;
            Status = status;
            Total = new Money(total);
            PaidAmount = new Money(paid);
        }
    }

    private sealed record SetupState(
        LoyaltyCommandHandler Handler,
        FakeCustomerRepository Repository,
        FakeUnitOfWork UnitOfWork,
        TestTransactionManager Transactions,
        TestCurrentUser Actor,
        Customer Customer,
        StoredOrder Order);
}
