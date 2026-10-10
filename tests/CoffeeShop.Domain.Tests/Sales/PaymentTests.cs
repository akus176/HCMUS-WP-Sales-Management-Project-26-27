namespace CoffeeShop.Domain.Tests.Sales;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class PaymentTests
{
    private static readonly UtcTimestamp Now = new(new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(PaymentMethod.Cash, null)]
    [InlineData(PaymentMethod.Card, "CARD-123")]
    [InlineData(PaymentMethod.EWallet, "WALLET-123")]
    public void RecordPayment_SupportsEveryPaymentMethod(string method, string? reference)
    {
        var order = CreateOrder();

        var payment = order.RecordPayment(
            EntityId.New(), method, new Money(45_000m), PaymentStatus.Succeeded, Now, EntityId.New(), reference);

        Assert.Equal(method, payment.Method);
        Assert.Equal(reference, payment.Reference);
        Assert.Equal(new Money(45_000m), order.PaidAmount);
        Assert.Equal(Money.Zero, order.OutstandingBalance);
        Assert.True(order.IsFullyPaid);
    }

    [Fact]
    public void PaidBalance_CountsOnlySucceededPayments()
    {
        var order = CreateOrder();

        order.RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(10_000m), PaymentStatus.Pending, Now, EntityId.New());
        order.RecordPayment(EntityId.New(), PaymentMethod.Card, new Money(15_000m), PaymentStatus.Failed, Now, EntityId.New(), "DECLINED-1");
        order.RecordPayment(EntityId.New(), PaymentMethod.EWallet, new Money(20_000m), PaymentStatus.Succeeded, Now, EntityId.New(), "WALLET-1");

        Assert.Equal(new Money(20_000m), order.PaidAmount);
        Assert.Equal(new Money(25_000m), order.OutstandingBalance);
        Assert.False(order.IsFullyPaid);
    }

    [Fact]
    public void Complete_RejectsPartialPaymentAndAcceptsFullPayment()
    {
        var order = CreateReadyOrder();
        order.RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(20_000m), PaymentStatus.Succeeded, Now, EntityId.New());

        Assert.Throws<InvalidOperationException>(() =>
            order.TransitionTo(OrderStatus.Completed, EntityId.New(), Now));
        Assert.Equal(OrderStatus.Ready, order.Status);

        order.RecordPayment(EntityId.New(), PaymentMethod.Card, new Money(25_000m), PaymentStatus.Succeeded, Now, EntityId.New(), "CARD-PAID");
        order.TransitionTo(OrderStatus.Completed, EntityId.New(), Now);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.True(order.IsFullyPaid);
    }

    [Theory]
    [InlineData("bank_transfer", PaymentStatus.Succeeded, "REF")]
    [InlineData(PaymentMethod.Card, "settled", "REF")]
    [InlineData(PaymentMethod.Card, PaymentStatus.Succeeded, null)]
    [InlineData(PaymentMethod.EWallet, PaymentStatus.Succeeded, " ")]
    public void RecordPayment_RejectsInvalidMethodStatusOrReference(string method, string status, string? reference)
    {
        var order = CreateOrder();

        Assert.ThrowsAny<ArgumentException>(() => order.RecordPayment(
            EntityId.New(), method, new Money(1m), status, Now, EntityId.New(), reference));
        Assert.Empty(order.Payments);
        Assert.Equal(Money.Zero, order.PaidAmount);
    }

    [Fact]
    public void RecordPayment_RejectsZeroAmountDuplicateIdentifierAndClosedOrder()
    {
        var order = CreateOrder();
        var paymentId = EntityId.New();

        Assert.Throws<ArgumentOutOfRangeException>(() => order.RecordPayment(
            EntityId.New(), PaymentMethod.Cash, Money.Zero, PaymentStatus.Succeeded, Now, EntityId.New()));
        order.RecordPayment(paymentId, PaymentMethod.Cash, new Money(45_000m), PaymentStatus.Succeeded, Now, EntityId.New());
        Assert.Throws<InvalidOperationException>(() => order.RecordPayment(
            paymentId, PaymentMethod.Cash, new Money(1m), PaymentStatus.Succeeded, Now, EntityId.New()));

        var ready = CreateReadyOrder();
        ready.RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(45_000m), PaymentStatus.Succeeded, Now, EntityId.New());
        ready.TransitionTo(OrderStatus.Completed, EntityId.New(), Now);
        Assert.Throws<InvalidOperationException>(() => ready.RecordPayment(
            EntityId.New(), PaymentMethod.Cash, new Money(1m), PaymentStatus.Succeeded, Now, EntityId.New()));
    }

    private static Order CreateReadyOrder()
    {
        var order = CreateOrder();
        var actor = EntityId.New();
        order.TransitionTo(OrderStatus.Confirmed, actor, Now);
        order.TransitionTo(OrderStatus.Preparing, actor, Now);
        order.TransitionTo(OrderStatus.Ready, actor, Now);
        return order;
    }

    private static Order CreateOrder()
    {
        var category = new Category(EntityId.New(), "Coffee", Now, Now);
        var item = new MenuItem(EntityId.New(), category, "Latte", Now, Now);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45_000m));
        var line = new OrderLine(EntityId.New(), item, variant, 1);
        return new Order(EntityId.New(), "ORD-PAY", "takeaway", EntityId.New(), Now, [line]);
    }
}
