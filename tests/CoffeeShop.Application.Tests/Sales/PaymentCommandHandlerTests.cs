namespace CoffeeShop.Application.Tests.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class PaymentCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 2, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(PaymentMethod.Cash, null)]
    [InlineData(PaymentMethod.Card, "CARD-001")]
    [InlineData(PaymentMethod.EWallet, "WALLET-001")]
    public async Task Record_SupportsEveryMethodAndReturnsBalance(string method, string? reference)
    {
        var setup = Setup();

        var result = await setup.Handler.RecordAsync(new(
            EntityId.New(), setup.Order.Id, method, 45_000m, PaymentStatus.Succeeded, reference));

        Assert.True(result.IsSuccess);
        Assert.Equal(45_000m, result.Value.PaidAmount);
        Assert.Equal(0m, result.Value.OutstandingBalance);
        Assert.True(result.Value.IsFullyPaid);
        Assert.False(result.Value.AlreadyRecorded);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(1, setup.Transactions.ExecutionCount);
        Assert.Equal(1, setup.Repository.OrderLockCount);
    }

    [Fact]
    public async Task Record_PartialPendingAndFailedPaymentsDoNotProduceFullPayment()
    {
        var setup = Setup();

        var partial = await setup.Handler.RecordAsync(new(
            EntityId.New(), setup.Order.Id, PaymentMethod.Cash, 20_000m));
        var pending = await setup.Handler.RecordAsync(new(
            EntityId.New(), setup.Order.Id, PaymentMethod.Card, 25_000m, PaymentStatus.Pending, "CARD-PENDING"));
        var failed = await setup.Handler.RecordAsync(new(
            EntityId.New(), setup.Order.Id, PaymentMethod.EWallet, 25_000m, PaymentStatus.Failed, "WALLET-FAILED"));

        Assert.Equal(25_000m, partial.Value.OutstandingBalance);
        Assert.False(partial.Value.IsFullyPaid);
        Assert.Equal(20_000m, pending.Value.PaidAmount);
        Assert.False(pending.Value.IsFullyPaid);
        Assert.Equal(20_000m, failed.Value.PaidAmount);
        Assert.False(failed.Value.IsFullyPaid);
    }

    [Fact]
    public async Task Record_RetryWithSameDataReturnsExistingWithoutDuplicateSave()
    {
        var setup = Setup();
        var paymentId = EntityId.New();
        var command = new RecordPaymentCommand(
            paymentId, setup.Order.Id, PaymentMethod.Card, 45_000m, PaymentStatus.Succeeded, " CARD-001 ");

        var first = await setup.Handler.RecordAsync(command);
        var retry = await setup.Handler.RecordAsync(command);

        Assert.False(first.Value.AlreadyRecorded);
        Assert.True(retry.Value.AlreadyRecorded);
        Assert.Equal(paymentId, retry.Value.PaymentId);
        Assert.Single(setup.Order.Payments);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(2, setup.Transactions.ExecutionCount);
    }

    [Fact]
    public async Task Record_RetryWithDifferentDataReturnsConflictWithoutMutation()
    {
        var setup = Setup();
        var paymentId = EntityId.New();
        Assert.True((await setup.Handler.RecordAsync(new(
            paymentId, setup.Order.Id, PaymentMethod.Cash, 20_000m))).IsSuccess);

        var result = await setup.Handler.RecordAsync(new(
            paymentId, setup.Order.Id, PaymentMethod.Cash, 30_000m));

        AssertError(result, ErrorType.Conflict, "payments.retry_mismatch");
        Assert.Single(setup.Order.Payments);
        Assert.Equal(20_000m, setup.Order.PaidAmount.Amount);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(PaymentMethod.Cash, 0, PaymentStatus.Succeeded, null)]
    [InlineData("bank", 1, PaymentStatus.Succeeded, "REF")]
    [InlineData(PaymentMethod.Card, 1, PaymentStatus.Succeeded, null)]
    [InlineData(PaymentMethod.EWallet, 1, "settled", "REF")]
    public async Task Record_InvalidPaymentReturnsSharedValidation(
        string method,
        decimal amount,
        string status,
        string? reference)
    {
        var setup = Setup();

        var result = await setup.Handler.RecordAsync(new(
            EntityId.New(), setup.Order.Id, method, amount, status, reference));

        AssertError(result, ErrorType.Validation, "payments.invalid");
        Assert.Empty(setup.Order.Payments);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Record_MissingOrderReturnsNotFound()
    {
        var setup = Setup(addOrder: false);

        var result = await setup.Handler.RecordAsync(new(
            EntityId.New(), setup.Order.Id, PaymentMethod.Cash, 1m));

        AssertError(result, ErrorType.NotFound, "payments.order_not_found");
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Record_UnauthenticatedActorDoesNotOpenTransaction()
    {
        var setup = Setup(authenticated: false);

        var result = await setup.Handler.RecordAsync(new(
            EntityId.New(), setup.Order.Id, PaymentMethod.Cash, 1m));

        AssertError(result, ErrorType.Authentication, "payments.authentication_required");
        Assert.Equal(0, setup.Transactions.ExecutionCount);
    }

    private static SetupState Setup(bool authenticated = true, bool addOrder = true)
    {
        var repository = new FakePaymentRepository();
        var order = CreateOrder();
        if (addOrder)
        {
            repository.Orders.Add(order.Id, order);
        }

        var unitOfWork = new FakeUnitOfWork();
        var transactions = new TestTransactionManager();
        var actor = authenticated ? TestCurrentUser.Authenticated() : TestCurrentUser.Unauthenticated();
        var handler = new PaymentCommandHandler(
            repository, unitOfWork, transactions, new TestClock(Now), actor);
        return new(handler, repository, unitOfWork, transactions, order);
    }

    private static Order CreateOrder()
    {
        var timestamp = new UtcTimestamp(Now);
        var category = new Category(EntityId.New(), "Coffee", timestamp, timestamp);
        var item = new MenuItem(EntityId.New(), category, "Latte", timestamp, timestamp);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45_000m));
        return new Order(
            EntityId.New(), "ORD-APP-PAY", "takeaway", EntityId.New(), timestamp,
            [new OrderLine(EntityId.New(), item, variant, 1)]);
    }

    private static void AssertError(Result result, ErrorType type, string code)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(type, result.Error!.Type);
        Assert.Equal(code, result.Error.Code);
    }

    private sealed record SetupState(
        PaymentCommandHandler Handler,
        FakePaymentRepository Repository,
        FakeUnitOfWork UnitOfWork,
        TestTransactionManager Transactions,
        Order Order);
}
