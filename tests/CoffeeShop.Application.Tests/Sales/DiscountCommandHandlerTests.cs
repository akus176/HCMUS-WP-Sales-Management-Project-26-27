namespace CoffeeShop.Application.Tests.Sales;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class DiscountCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 2, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(DiscountType.Fixed)]
    [InlineData(DiscountType.Percentage)]
    public async Task RequestAndApproval_RecordCurrentActorsAndOnlyApprovalChangesTotals(string type)
    {
        var setup = Setup();
        var requester = setup.Actor.Id;
        var id = EntityId.New();
        var requested = await setup.Handler.RequestAsync(new(id, setup.Order.Id, type, 10m, "Reason"));
        Assert.True(requested.IsSuccess);
        Assert.Equal(DiscountStatus.Pending, requested.Value.Status);
        Assert.Equal(100m, requested.Value.Total);
        Assert.Null(requested.Value.ComputedAmount);

        setup.Actor.Id = EntityId.New();
        setup.Actor.AddPermission("discounts.approve");
        setup.Clock.Advance(TimeSpan.FromMinutes(1));
        var approved = await setup.Handler.ApproveAsync(new(id));

        Assert.True(approved.IsSuccess);
        Assert.Equal(90m, approved.Value.Total);
        Assert.Equal(10m, approved.Value.DiscountTotal);
        var discount = Assert.Single(setup.Order.Discounts);
        Assert.Equal(requester, discount.RequestedBy);
        Assert.Equal(new UtcTimestamp(Now), discount.RequestedAt);
        Assert.Equal(setup.Actor.Id, discount.ApprovedBy);
        Assert.Equal(setup.Clock.UtcNow, discount.ApprovedAt);
        Assert.Equal(2, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(2, setup.Transactions.ExecutionCount);
        Assert.Equal(2, setup.Repository.OrderLockCount);
    }

    [Theory]
    [InlineData("request", false)]
    [InlineData("approve", false)]
    [InlineData("reject", false)]
    [InlineData("request", true)]
    [InlineData("approve", true)]
    [InlineData("reject", true)]
    public async Task UnauthorizedOperations_DoNotLoadOrSave(string operation, bool authenticated)
    {
        var setup = Setup();
        var denied = authenticated ? TestCurrentUser.Authenticated(roles: ["Manager"]) : TestCurrentUser.Unauthenticated();
        var handler = new DiscountCommandHandler(setup.Repository, setup.UnitOfWork, setup.Transactions, setup.Clock, denied);
        var id = EntityId.New();
        var result = operation switch
        {
            "request" => await handler.RequestAsync(new(id, setup.Order.Id, DiscountType.Fixed, 10m, "Reason")),
            "approve" => await handler.ApproveAsync(new(id)),
            _ => await handler.RejectAsync(new(id))
        };

        AssertError(result, authenticated ? ErrorType.Authorization : ErrorType.Authentication);
        Assert.Equal(0, setup.Transactions.ExecutionCount);
        Assert.Equal(0, setup.Repository.OrderLockCount);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Equal(100m, setup.Order.Total.Amount);
    }

    [Fact]
    public async Task RequestPermission_DoesNotGrantApprovalOrRejection()
    {
        var setup = Setup();
        var id = EntityId.New();
        await setup.Handler.RequestAsync(new(id, setup.Order.Id, DiscountType.Fixed, 10m, "Reason"));
        AssertError(await setup.Handler.ApproveAsync(new(id)), ErrorType.Authorization);
        AssertError(await setup.Handler.RejectAsync(new(id)), ErrorType.Authorization);
        Assert.Equal(DiscountStatus.Pending, Assert.Single(setup.Order.Discounts).Status);
        Assert.Equal(100m, setup.Order.Total.Amount);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("unknown", 10, "Reason", ErrorType.Validation)]
    [InlineData(DiscountType.Fixed, 0, "Reason", ErrorType.Validation)]
    [InlineData(DiscountType.Fixed, 1.001, "Reason", ErrorType.Validation)]
    [InlineData(DiscountType.Percentage, 101, "Reason", ErrorType.Validation)]
    [InlineData(DiscountType.Fixed, 10, " ", ErrorType.Validation)]
    [InlineData(DiscountType.Fixed, 101, "Reason", ErrorType.BusinessRule)]
    public async Task InvalidRequest_ReturnsSharedErrorWithoutSaving(string type, decimal value, string reason, ErrorType errorType)
    {
        var setup = Setup();
        var result = await setup.Handler.RequestAsync(new(EntityId.New(), setup.Order.Id, type, value, reason));
        AssertError(result, errorType);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
        Assert.Empty(setup.Order.Discounts);
        Assert.Equal(100m, setup.Order.Total.Amount);
    }

    [Fact]
    public async Task Approval_RevalidatesAfterLoyaltyAndDoesNotSaveFailure()
    {
        var setup = Setup();
        var id = EntityId.New();
        await setup.Handler.RequestAsync(new(id, setup.Order.Id, DiscountType.Fixed, 90m, "Reason"));
        setup.Order.ApplyLoyaltyRedemption(20, new Money(20m));
        setup.Actor.AddPermission("discounts.approve");

        AssertError(await setup.Handler.ApproveAsync(new(id)), ErrorType.BusinessRule);
        Assert.Equal(DiscountStatus.Pending, Assert.Single(setup.Order.Discounts).Status);
        Assert.Equal(80m, setup.Order.Total.Amount);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DecisionRetries_ReturnConflictAndNeverApplyTwice()
    {
        var setup = Setup();
        var id = EntityId.New();
        await setup.Handler.RequestAsync(new(id, setup.Order.Id, DiscountType.Fixed, 10m, "Reason"));
        setup.Actor.AddPermission("discounts.approve");
        Assert.True((await setup.Handler.ApproveAsync(new(id))).IsSuccess);
        AssertError(await setup.Handler.ApproveAsync(new(id)), ErrorType.Conflict);
        AssertError(await setup.Handler.RejectAsync(new(id)), ErrorType.Conflict);
        Assert.Equal(90m, setup.Order.Total.Amount);
        Assert.Equal(2, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task Rejection_PreservesTotalAndRecordsActorAndTime()
    {
        var setup = Setup();
        var id = EntityId.New();
        await setup.Handler.RequestAsync(new(id, setup.Order.Id, DiscountType.Percentage, 10m, "Reason"));
        setup.Actor.AddPermission("discounts.approve");
        var result = await setup.Handler.RejectAsync(new(id));
        Assert.True(result.IsSuccess);
        Assert.Equal(DiscountStatus.Rejected, result.Value.Status);
        Assert.Equal(100m, result.Value.Total);
        Assert.Null(result.Value.ComputedAmount);
        var rejected = Assert.Single(setup.Order.Discounts);
        Assert.Equal(setup.Actor.Id, rejected.RejectedBy);
        Assert.Equal(setup.Clock.UtcNow, rejected.RejectedAt);
        AssertError(await setup.Handler.ApproveAsync(new(id)), ErrorType.Conflict);
    }

    [Fact]
    public async Task MissingOrderOrDiscount_ReturnsNotFound()
    {
        var setup = Setup();
        AssertError(await setup.Handler.RequestAsync(new(EntityId.New(), EntityId.New(), DiscountType.Fixed, 10m, "Reason")), ErrorType.NotFound);
        setup.Actor.AddPermission("discounts.approve");
        AssertError(await setup.Handler.ApproveAsync(new(EntityId.New())), ErrorType.NotFound);
        AssertError(await setup.Handler.RejectAsync(new(EntityId.New())), ErrorType.NotFound);
        Assert.Equal(0, setup.UnitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DuplicateIdentifier_ReturnsConflictForSameOrOtherOrder()
    {
        var setup = Setup();
        var command = new RequestDiscountCommand(EntityId.New(), setup.Order.Id, DiscountType.Fixed, 10m, "Reason");
        Assert.True((await setup.Handler.RequestAsync(command)).IsSuccess);
        AssertError(await setup.Handler.RequestAsync(command), ErrorType.Conflict);
        var otherId = EntityId.New();
        setup.Repository.OtherDiscounts.Add(otherId, EntityId.New());
        AssertError(await setup.Handler.RequestAsync(command with { DiscountId = otherId }), ErrorType.Conflict);
        Assert.Single(setup.Order.Discounts);
        Assert.Equal(1, setup.UnitOfWork.SaveChangesCallCount);
    }

    private static SetupState Setup()
    {
        var timestamp = new UtcTimestamp(Now);
        var category = new Category(EntityId.New(), "Coffee", timestamp, timestamp);
        var item = new MenuItem(EntityId.New(), category, "Latte", timestamp, timestamp);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(100m));
        var order = new Order(EntityId.New(), "ORD-APP-DISCOUNT", "takeaway", EntityId.New(), timestamp,
            [new OrderLine(EntityId.New(), item, variant, 1)]);
        var repository = new FakeDiscountRepository();
        repository.Orders.Add(order.Id, order);
        var unitOfWork = new FakeUnitOfWork();
        var transactions = new TestTransactionManager();
        var clock = new TestClock(Now);
        var actor = TestCurrentUser.Authenticated(permissions: ["discounts.request"]);
        var handler = new DiscountCommandHandler(repository, unitOfWork, transactions, clock, actor);
        return new(handler, repository, unitOfWork, transactions, clock, actor, order);
    }

    private static void AssertError(Result result, ErrorType type)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(type, result.Error!.Type);
    }

    private sealed record SetupState(DiscountCommandHandler Handler, FakeDiscountRepository Repository,
        FakeUnitOfWork UnitOfWork, TestTransactionManager Transactions, TestClock Clock, TestCurrentUser Actor, Order Order);
}
