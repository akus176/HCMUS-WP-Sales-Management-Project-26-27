namespace CoffeeShop.Application.Tests.Reporting;

using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class RevenueRecognitionRuleTests
{
    private static readonly EntityId ActorId = new(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "Coffee shop UTC+07", TimeSpan.FromHours(7), "Coffee shop UTC+07", "Coffee shop UTC+07");

    [Fact]
    public void Recognize_IncludesOnlySuccessfulPaymentsForAFullyPaidCompletedOrder()
    {
        var order = CreateReadyOrder(100m);
        RecordPayment(order, 10m, PaymentStatus.Pending, AtUtc(2026, 10, 15, 2));
        RecordPayment(order, 15m, PaymentStatus.Failed, AtUtc(2026, 10, 15, 3));
        RecordPayment(order, 40m, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 4));
        RecordPayment(order, 60m, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 5));
        Complete(order, AtUtc(2026, 10, 15, 6));

        var recognized = RevenueRecognitionRule.Recognize(order, Period(2026, 10, 15));

        Assert.Equal([40m, 60m], recognized.Select(payment => payment.Amount.Amount));
        Assert.Equal(100m, recognized.Sum(payment => payment.Amount.Amount));
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Cancelled)]
    public void Recognize_ExcludesOrdersThatAreNotCompleted(string status)
    {
        var order = CreateOrderInStatus(status, 100m);

        var recognized = RevenueRecognitionRule.Recognize(order, Period(2026, 10, 15));

        Assert.Empty(recognized);
    }

    [Fact]
    public void Recognize_ExcludesAnOrderWhoseInventoryWasRestored()
    {
        var order = CreateCompletedOrder(100m, (100m, AtUtc(2026, 10, 15, 4)));

        var recognized = RevenueRecognitionRule.Recognize(
            order, Period(2026, 10, 15), inventoryWasRestored: true);

        Assert.Empty(recognized);
    }

    [Fact]
    public void Recognize_UsesTheDiscountedPayableTotalAndNeverCountsOverpayment()
    {
        var order = CreateReadyOrder(100m);
        var discount = order.RequestDiscount(
            EntityId.New(), DiscountType.Percentage, 25m, "Promotion", ActorId, AtUtc(2026, 10, 15, 1));
        order.ApproveDiscount(discount.Id, ActorId, AtUtc(2026, 10, 15, 2));
        RecordPayment(order, 100m, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 3));
        Complete(order, AtUtc(2026, 10, 15, 4));

        var recognized = Assert.Single(RevenueRecognitionRule.Recognize(order, Period(2026, 10, 15)));

        Assert.Equal(75m, order.Total.Amount);
        Assert.Equal(75m, recognized.Amount.Amount);
    }

    [Fact]
    public void Recognize_PendingDiscountDoesNotChangeRevenue()
    {
        var order = CreateReadyOrder(100m);
        order.RequestDiscount(EntityId.New(), DiscountType.Fixed, 20m, "Awaiting approval", ActorId,
            AtUtc(2026, 10, 15, 1));
        RecordPayment(order, 100m, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 2));
        Complete(order, AtUtc(2026, 10, 15, 3));

        var recognized = Assert.Single(RevenueRecognitionRule.Recognize(order, Period(2026, 10, 15)));

        Assert.Equal(100m, order.Total.Amount);
        Assert.Equal(100m, recognized.Amount.Amount);
    }

    [Fact]
    public void Recognize_CompletedOrderDiscountedToZeroHasZeroRevenue()
    {
        var order = CreateReadyOrder(100m);
        var discount = order.RequestDiscount(
            EntityId.New(), DiscountType.Percentage, 100m, "Complimentary", ActorId, AtUtc(2026, 10, 15, 1));
        order.ApproveDiscount(discount.Id, ActorId, AtUtc(2026, 10, 15, 2));
        Complete(order, AtUtc(2026, 10, 15, 3));

        var recognized = RevenueRecognitionRule.Recognize(order, Period(2026, 10, 15));

        Assert.Equal(Money.Zero, order.Total);
        Assert.Empty(recognized);
    }

    [Fact]
    public void Recognize_AllocatesThePayableCapBeforeFilteringThePeriod()
    {
        var order = CreateReadyOrder(100m);
        RecordPayment(order, 80m, PaymentStatus.Succeeded, AtUtc(2026, 10, 14, 16, 59, 59));
        RecordPayment(order, 40m, PaymentStatus.Succeeded, AtUtc(2026, 10, 14, 17));
        Complete(order, AtUtc(2026, 10, 15, 1));

        var recognized = Assert.Single(RevenueRecognitionRule.Recognize(order, Period(2026, 10, 15)));

        Assert.Equal(20m, recognized.Amount.Amount);
    }

    [Fact]
    public void RevenuePeriod_UsesInclusiveStartAndExclusiveEndInTheBusinessTimeZone()
    {
        var period = Period(2026, 10, 15);

        Assert.Equal(AtUtc(2026, 10, 14, 17).Value, period.StartUtc);
        Assert.Equal(AtUtc(2026, 10, 15, 17).Value, period.EndUtcExclusive);
        Assert.True(period.Contains(AtUtc(2026, 10, 14, 17)));
        Assert.True(period.Contains(AtUtc(2026, 10, 15, 16, 59, 59)));
        Assert.False(period.Contains(AtUtc(2026, 10, 14, 16, 59, 59)));
        Assert.False(period.Contains(AtUtc(2026, 10, 15, 17)));
    }

    [Fact]
    public void RevenuePeriod_RejectsAReversedDateRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RevenuePeriod.FromLocalDates(
            new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 15), BusinessTimeZone));
    }

    [Fact]
    public void Recognize_RejectsACompletedButUnderpaidProjection()
    {
        var order = new PersistedOrderProjection(100m, 99m, AtUtc(2026, 10, 15, 6));

        var recognized = RevenueRecognitionRule.Recognize(order, Period(2026, 10, 15));

        Assert.Empty(recognized);
    }

    private static RevenuePeriod Period(int year, int month, int day) =>
        RevenuePeriod.FromLocalDates(new DateOnly(year, month, day), new DateOnly(year, month, day), BusinessTimeZone);

    private static Order CreateCompletedOrder(decimal total, params (decimal Amount, UtcTimestamp PaidAt)[] payments)
    {
        var order = CreateReadyOrder(total);
        foreach (var payment in payments)
        {
            RecordPayment(order, payment.Amount, PaymentStatus.Succeeded, payment.PaidAt);
        }

        var completedAt = payments.Max(payment => payment.PaidAt.Value).AddHours(1);
        Complete(order, new UtcTimestamp(completedAt));
        return order;
    }

    private static Order CreateOrderInStatus(string status, decimal total)
    {
        var order = CreateOrder(total);
        RecordPayment(order, total, PaymentStatus.Succeeded, AtUtc(2026, 10, 15, 2));
        if (status == OrderStatus.Draft)
        {
            return order;
        }

        order.TransitionTo(OrderStatus.Confirmed, ActorId, AtUtc(2026, 10, 15, 3));
        if (status == OrderStatus.Confirmed)
        {
            return order;
        }

        if (status == OrderStatus.Cancelled)
        {
            order.TransitionTo(OrderStatus.Cancelled, ActorId, AtUtc(2026, 10, 15, 4), "Changed mind");
            return order;
        }

        order.TransitionTo(OrderStatus.Preparing, ActorId, AtUtc(2026, 10, 15, 4));
        if (status == OrderStatus.Preparing)
        {
            return order;
        }

        order.TransitionTo(OrderStatus.Ready, ActorId, AtUtc(2026, 10, 15, 5));
        return order;
    }

    private static Order CreateReadyOrder(decimal total)
    {
        var order = CreateOrder(total);
        order.TransitionTo(OrderStatus.Confirmed, ActorId, AtUtc(2026, 10, 15, 0));
        order.TransitionTo(OrderStatus.Preparing, ActorId, AtUtc(2026, 10, 15, 0, 10));
        order.TransitionTo(OrderStatus.Ready, ActorId, AtUtc(2026, 10, 15, 0, 20));
        return order;
    }

    private static Order CreateOrder(decimal total)
    {
        var createdAt = AtUtc(2026, 10, 14, 16);
        var category = new Category(EntityId.New(), "Coffee", createdAt, createdAt);
        var menuItem = new MenuItem(EntityId.New(), category, "Latte", createdAt, createdAt);
        var variant = new SizeVariant(EntityId.New(), menuItem.Id, "Regular", new Money(total));
        var line = new OrderLine(EntityId.New(), menuItem, variant, 1);
        return new Order(EntityId.New(), "ORD-REV", "takeaway", ActorId, createdAt, [line]);
    }

    private static void RecordPayment(Order order, decimal amount, string status, UtcTimestamp paidAt) =>
        order.RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(amount), status, paidAt, ActorId);

    private static void Complete(Order order, UtcTimestamp completedAt) =>
        order.TransitionTo(OrderStatus.Completed, ActorId, completedAt);

    private static UtcTimestamp AtUtc(int year, int month, int day, int hour, int minute = 0, int second = 0) =>
        new(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));

    private sealed class PersistedOrderProjection : Order
    {
        public PersistedOrderProjection(decimal total, decimal paid, UtcTimestamp completedAt)
        {
            Id = EntityId.New();
            Total = new Money(total);
            PaidAmount = new Money(paid);
            Status = OrderStatus.Completed;
            CompletedAt = completedAt;
        }
    }
}
