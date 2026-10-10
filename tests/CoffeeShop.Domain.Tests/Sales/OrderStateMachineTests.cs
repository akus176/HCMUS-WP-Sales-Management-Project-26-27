namespace CoffeeShop.Domain.Tests.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public class OrderStateMachineTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void TransitionTo_RecordsEveryAllowedTransitionWithActorAndTimestamp()
    {
        var actor = EntityId.New();
        var order = CreateOrder();
        var times = Enumerable.Range(1, 4).Select(hours => new UtcTimestamp(Now.Value.AddHours(hours))).ToArray();

        order.TransitionTo(OrderStatus.Confirmed, actor, times[0]);
        order.TransitionTo(OrderStatus.Preparing, actor, times[1]);
        order.TransitionTo(OrderStatus.Ready, actor, times[2]);
        order.RecordPayment(
            EntityId.New(), PaymentMethod.Cash, order.Total, PaymentStatus.Succeeded, times[2], actor);
        var completed = order.TransitionTo(OrderStatus.Completed, actor, times[3]);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(times[3], order.CompletedAt);
        Assert.Equal(4, order.StatusHistory.Count);
        Assert.Equal(OrderStatus.Ready, completed.FromStatus);
        Assert.Equal(OrderStatus.Completed, completed.ToStatus);
        Assert.Equal(actor, completed.ChangedBy);
        Assert.Equal(times[3], completed.ChangedAt);
    }

    [Theory]
    [InlineData("draft", "preparing")]
    [InlineData("confirmed", "ready")]
    [InlineData("ready", "preparing")]
    [InlineData("completed", "cancelled")]
    [InlineData("cancelled", "draft")]
    public void TransitionTo_RejectsInvalidTransitionsWithoutHistory(string first, string target)
    {
        var order = CreateOrder();
        if (first != OrderStatus.Draft)
        {
            order.TransitionTo(OrderStatus.Confirmed, EntityId.New(), Now);
            if (first is OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Completed)
            {
                order.TransitionTo(OrderStatus.Preparing, EntityId.New(), new UtcTimestamp(Now.Value.AddHours(1)));
            }
            if (first is OrderStatus.Ready or OrderStatus.Completed)
            {
                order.TransitionTo(OrderStatus.Ready, EntityId.New(), new UtcTimestamp(Now.Value.AddHours(2)));
            }
            if (first == OrderStatus.Completed)
            {
                order.RecordPayment(
                    EntityId.New(), PaymentMethod.Cash, order.Total, PaymentStatus.Succeeded, Now, EntityId.New());
                order.TransitionTo(OrderStatus.Completed, EntityId.New(), new UtcTimestamp(Now.Value.AddHours(3)));
            }
        }

        var count = order.StatusHistory.Count;
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(target, EntityId.New(), Now));
        Assert.Equal(count, order.StatusHistory.Count);
    }

    [Fact]
    public void TransitionTo_CancellationRequiresReasonAndRecordsIt()
    {
        var order = CreateOrder();

        Assert.Throws<ArgumentException>(() => order.TransitionTo(OrderStatus.Cancelled, EntityId.New(), Now));
        var history = order.TransitionTo(OrderStatus.Cancelled, EntityId.New(), Now, " Customer request ");

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal("Customer request", order.CancelReason);
        Assert.Equal(OrderStatus.Cancelled, history.ToStatus);
        Assert.Equal(Now, order.CancelledAt);
    }

    [Fact]
    public void ApplyLoyaltyRedemption_ReducesTotalAndRecordsSnapshot()
    {
        var order = CreateOrder();

        order.ApplyLoyaltyRedemption(100, new Money(5000m));

        Assert.Equal(100, order.LoyaltyPointsRedeemed);
        Assert.Equal(new Money(5000m), order.LoyaltyDiscountAmount);
        Assert.Equal(new Money(40000m), order.Total);
    }

    [Fact]
    public void ApplyLoyaltyRedemption_RejectsDuplicateOrIneligibleChangesWithoutFurtherMutation()
    {
        var order = CreateOrder();
        order.ApplyLoyaltyRedemption(100, new Money(5000m));

        Assert.Throws<InvalidOperationException>(() => order.ApplyLoyaltyRedemption(50, new Money(2500m)));
        Assert.Equal(new Money(40000m), order.Total);
        Assert.Equal(100, order.LoyaltyPointsRedeemed);

        var completed = CreateOrder();
        var actor = EntityId.New();
        completed.TransitionTo(OrderStatus.Confirmed, actor, Now);
        completed.TransitionTo(OrderStatus.Preparing, actor, Now);
        completed.TransitionTo(OrderStatus.Ready, actor, Now);
        completed.RecordPayment(
            EntityId.New(), PaymentMethod.Cash, completed.Total, PaymentStatus.Succeeded, Now, actor);
        completed.TransitionTo(OrderStatus.Completed, actor, Now);
        Assert.Throws<InvalidOperationException>(() => completed.ApplyLoyaltyRedemption(10, new Money(100m)));
        Assert.Equal(new Money(45000m), completed.Total);
    }

    private static Order CreateOrder()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", Now, Now);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));
        var line = new OrderLine(EntityId.New(), item, variant, 1);
        return new Order(EntityId.New(), "ORD-STATE", "takeaway", EntityId.New(), Now, [line]);
    }
}
