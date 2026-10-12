namespace CoffeeShop.Domain.Tests.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class DiscountTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(DiscountType.Fixed, 10)]
    [InlineData(DiscountType.Percentage, 10)]
    public void Request_RecordsReasonAndActorWithoutChangingTotals(string type, decimal value)
    {
        var order = CreateOrder();
        var actor = EntityId.New();
        var discount = order.RequestDiscount(EntityId.New(), type, value, " Service recovery ", actor, Now);

        Assert.Equal(type, discount.Type);
        Assert.Equal(value, discount.Value);
        Assert.Equal("Service recovery", discount.Reason);
        Assert.Equal(actor, discount.RequestedBy);
        Assert.Equal(Now, discount.RequestedAt);
        Assert.Equal(DiscountStatus.Pending, discount.Status);
        Assert.Null(discount.ComputedAmount);
        Assert.Null(discount.ApprovedBy);
        Assert.Null(discount.ApprovedAt);
        Assert.Equal(100m, order.Total.Amount);
        Assert.Equal(Money.Zero, order.DiscountTotal);
    }

    [Theory]
    [InlineData(DiscountType.Fixed, 10, 90)]
    [InlineData(DiscountType.Percentage, 10, 90)]
    [InlineData(DiscountType.Fixed, 100, 0)]
    [InlineData(DiscountType.Percentage, 100, 0)]
    [InlineData(DiscountType.Fixed, 0.01, 99.99)]
    public void Approval_ChangesTotalsAndStoresDecision(string type, decimal value, decimal expectedTotal)
    {
        var order = CreateOrder();
        var request = Request(order, type, value);
        var manager = EntityId.New();

        var approved = order.ApproveDiscount(request.Id, manager, Now);

        Assert.Same(request, approved);
        Assert.Equal(DiscountStatus.Approved, approved.Status);
        Assert.Equal(manager, approved.ApprovedBy);
        Assert.Equal(Now, approved.ApprovedAt);
        Assert.Equal(100m - expectedTotal, approved.ComputedAmount!.Amount);
        Assert.Equal(100m - expectedTotal, order.DiscountTotal.Amount);
        Assert.Equal(expectedTotal, order.Total.Amount);
        Assert.Equal(expectedTotal, order.OutstandingBalance.Amount);
        Assert.Equal(100m, order.Subtotal.Amount);
    }

    [Fact]
    public void Approval_RecalculatesPercentageAfterPriorApprovalAndLoyalty()
    {
        var order = CreateOrder();
        var percentage = Request(order, DiscountType.Percentage, 50m);
        var fixedDiscount = Request(order, DiscountType.Fixed, 20m);
        order.ApproveDiscount(fixedDiscount.Id, EntityId.New(), Now);
        order.ApplyLoyaltyRedemption(10, new Money(10m));

        order.ApproveDiscount(percentage.Id, EntityId.New(), Now);

        Assert.Equal(35m, percentage.ComputedAmount!.Amount);
        Assert.Equal(55m, order.DiscountTotal.Amount);
        Assert.Equal(10m, order.LoyaltyDiscountAmount.Amount);
        Assert.Equal(35m, order.Total.Amount);
        Assert.Equal(order.Subtotal.Amount - order.DiscountTotal.Amount - order.LoyaltyDiscountAmount.Amount, order.Total.Amount);
    }

    [Fact]
    public void Percentage_RoundsDownAndRejectsAnAmountBelowOneCent()
    {
        var order = CreateOrder(99.99m);
        var percentage = Request(order, DiscountType.Percentage, 10m);
        order.ApproveDiscount(percentage.Id, EntityId.New(), Now);
        Assert.Equal(new Money(9.99m), percentage.ComputedAmount);
        Assert.Equal(new Money(90m), order.Total);

        var tinyOrder = CreateOrder(0.01m);
        Assert.Throws<ArgumentOutOfRangeException>(() => Request(tinyOrder, DiscountType.Percentage, 1m));
        Assert.Empty(tinyOrder.Discounts);
    }

    [Theory]
    [InlineData("other", 10, "Reason")]
    [InlineData(DiscountType.Fixed, 0, "Reason")]
    [InlineData(DiscountType.Fixed, -1, "Reason")]
    [InlineData(DiscountType.Fixed, 100.01, "Reason")]
    [InlineData(DiscountType.Fixed, 1.001, "Reason")]
    [InlineData(DiscountType.Fixed, 10000000000, "Reason")]
    [InlineData(DiscountType.Percentage, 100.01, "Reason")]
    [InlineData(DiscountType.Percentage, 1.001, "Reason")]
    [InlineData(DiscountType.Fixed, 10, " ")]
    [InlineData(DiscountType.Fixed, 10, null)]
    public void InvalidRequests_LeaveOrderUnchanged(string type, decimal value, string? reason)
    {
        var order = CreateOrder();
        Assert.ThrowsAny<ArgumentException>(() =>
            order.RequestDiscount(EntityId.New(), type, value, reason!, EntityId.New(), Now));
        Assert.Empty(order.Discounts);
        Assert.Equal(new Money(100m), order.Total);
        Assert.Equal(Money.Zero, order.DiscountTotal);
    }

    [Fact]
    public void Approval_RechecksExcessiveFixedDiscountWithoutPartialMutation()
    {
        var order = CreateOrder();
        var first = Request(order, DiscountType.Fixed, 60m);
        var second = Request(order, DiscountType.Fixed, 60m);
        order.ApproveDiscount(first.Id, EntityId.New(), Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => order.ApproveDiscount(second.Id, EntityId.New(), Now));
        Assert.Equal(DiscountStatus.Pending, second.Status);
        Assert.Null(second.ApprovedBy);
        Assert.Null(second.ComputedAmount);
        Assert.Equal(new Money(40m), order.Total);
        Assert.Equal(new Money(60m), order.DiscountTotal);
    }

    [Fact]
    public void Rejection_IsAuditedAndCannotLaterBeApproved()
    {
        var order = CreateOrder();
        var request = Request(order);
        var manager = EntityId.New();
        order.RejectDiscount(request.Id, manager, Now);

        Assert.Equal(DiscountStatus.Rejected, request.Status);
        Assert.Equal(manager, request.RejectedBy);
        Assert.Equal(Now, request.RejectedAt);
        Assert.Null(request.ComputedAmount);
        Assert.Null(request.ApprovedBy);
        Assert.Equal(new Money(100m), order.Total);
        Assert.Equal(Money.Zero, order.DiscountTotal);
        Assert.Throws<InvalidOperationException>(() => order.ApproveDiscount(request.Id, manager, Now));
        Assert.Throws<InvalidOperationException>(() => order.RejectDiscount(request.Id, manager, Now));
    }

    [Fact]
    public void Approval_CannotBeAppliedTwiceOrReversedByRejection()
    {
        var order = CreateOrder();
        var request = Request(order);
        order.ApproveDiscount(request.Id, EntityId.New(), Now);

        Assert.Throws<InvalidOperationException>(() => order.ApproveDiscount(request.Id, EntityId.New(), Now));
        Assert.Throws<InvalidOperationException>(() => order.RejectDiscount(request.Id, EntityId.New(), Now));
        Assert.Equal(new Money(90m), order.Total);
        Assert.Equal(new Money(10m), order.DiscountTotal);
    }

    [Fact]
    public void InvalidActorsTimesAndOrderMembership_LeavePendingRequestUnchanged()
    {
        var order = CreateOrder();
        var request = Request(order);
        var earlier = new UtcTimestamp(Now.Value.AddSeconds(-1));
        Assert.Throws<ArgumentException>(() => order.ApproveDiscount(request.Id, EntityId.New(), earlier));
        Assert.Throws<ArgumentException>(() => order.RejectDiscount(request.Id, EntityId.New(), earlier));
        Assert.Throws<ArgumentNullException>(() => order.ApproveDiscount(request.Id, null!, Now));
        Assert.Throws<ArgumentException>(() => CreateOrder().ApproveDiscount(request.Id, EntityId.New(), Now));
        Assert.Throws<InvalidOperationException>(() =>
            order.RequestDiscount(request.Id, DiscountType.Fixed, 10m, "Duplicate", EntityId.New(), Now));
        Assert.Throws<ArgumentException>(() =>
            order.RequestDiscount(EntityId.New(), DiscountType.Fixed, 10m, "Earlier", EntityId.New(), earlier));
        Assert.Equal(DiscountStatus.Pending, request.Status);
        Assert.Equal(new Money(100m), order.Total);
        Assert.Single(order.Discounts);
    }

    [Theory]
    [InlineData(OrderStatus.Completed)]
    [InlineData(OrderStatus.Cancelled)]
    public void ClosedOrders_RejectRequestsAndDecisions(string status)
    {
        var order = CreateOrder();
        var request = Request(order);
        var actor = EntityId.New();
        if (status == OrderStatus.Completed)
        {
            order.RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(100m), PaymentStatus.Succeeded, Now, actor);
            order.TransitionTo(OrderStatus.Confirmed, actor, Now);
            order.TransitionTo(OrderStatus.Preparing, actor, Now);
            order.TransitionTo(OrderStatus.Ready, actor, Now);
        }
        order.TransitionTo(status, actor, Now, "Cancelled");

        Assert.Throws<InvalidOperationException>(() => Request(order));
        Assert.Throws<InvalidOperationException>(() => order.ApproveDiscount(request.Id, actor, Now));
        Assert.Throws<InvalidOperationException>(() => order.RejectDiscount(request.Id, actor, Now));
        Assert.Equal(DiscountStatus.Pending, request.Status);
        Assert.Equal(new Money(100m), order.Total);
    }

    [Fact]
    public void ApprovedDiscount_UpdatesBalanceWithoutChangingRecordedPayment()
    {
        var order = CreateOrder();
        var request = Request(order, DiscountType.Fixed, 20m);
        order.RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(80m), PaymentStatus.Succeeded, Now, EntityId.New());
        Assert.False(order.IsFullyPaid);
        order.ApproveDiscount(request.Id, EntityId.New(), Now);
        Assert.True(order.IsFullyPaid);
        Assert.Equal(new Money(80m), order.PaidAmount);
        Assert.Equal(Money.Zero, order.OutstandingBalance);
    }

    private static Discount Request(Order order, string type = DiscountType.Fixed, decimal value = 10m) =>
        order.RequestDiscount(EntityId.New(), type, value, "Service recovery", EntityId.New(), Now);

    private static Order CreateOrder(decimal subtotal = 100m)
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", Now, Now);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(subtotal));
        return new Order(EntityId.New(), "ORD-DISCOUNT", "takeaway", EntityId.New(), Now,
            [new OrderLine(EntityId.New(), item, variant, 1)]);
    }
}
