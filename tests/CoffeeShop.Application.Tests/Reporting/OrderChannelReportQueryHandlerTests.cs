namespace CoffeeShop.Application.Tests.Reporting;

using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class OrderChannelReportQueryHandlerTests
{
    private static readonly EntityId ActorId = EntityId.New();
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "Coffee shop UTC+07", TimeSpan.FromHours(7), "Coffee shop UTC+07", "Coffee shop UTC+07");

    [Fact]
    public async Task GetChannels_ReconcilesAllChannelsWithRevenueDespiteSplitPaymentsAndDiscounts()
    {
        var dineIn = ConfirmedOrder("dine_in", 100m);
        Pay(dineIn, 60m, PaymentMethod.Cash, AtUtc(2026, 10, 1, 1));
        Pay(dineIn, 50m, PaymentMethod.Card, AtUtc(2026, 10, 2, 1));
        dineIn.TransitionTo(OrderStatus.Completed, ActorId, AtUtc(2026, 10, 2, 2));

        var takeaway = ConfirmedOrder("takeaway", 100m);
        var discount = takeaway.RequestDiscount(
            EntityId.New(), DiscountType.Fixed, 20m, "Promotion", ActorId, AtUtc(2026, 10, 1, 0));
        takeaway.ApproveDiscount(discount.Id, ActorId, AtUtc(2026, 10, 1, 1));
        takeaway.ApplyLoyaltyRedemption(10, new Money(10m));
        takeaway.RequestDiscount(
            EntityId.New(), DiscountType.Fixed, 10m, "Pending approval", ActorId, AtUtc(2026, 10, 1, 2));
        Pay(takeaway, 100m, PaymentMethod.Card, AtUtc(2026, 10, 2, 1));
        takeaway.TransitionTo(OrderStatus.Completed, ActorId, AtUtc(2026, 10, 2, 2));

        var delivery = ConfirmedOrder("delivery", 30m);
        Pay(delivery, 30m, PaymentMethod.EWallet, AtUtc(2026, 10, 2, 1));
        delivery.TransitionTo(OrderStatus.Completed, ActorId, AtUtc(2026, 10, 2, 2));
        var repository = new StubRepository(new(delivery, false), new(dineIn, false), new(takeaway, false));

        var result = await new OrderChannelReportQueryHandler(repository).GetChannelsAsync(OctoberQuery());
        var revenue = await new RevenueReportQueryHandler(repository).GetRevenueAsync(new(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), BusinessTimeZone));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.True(revenue.IsSuccess, revenue.Error?.Description);
        Assert.Equal(
            [new OrderChannelRevenueBreakdown("dine_in", 100m, 1),
             new OrderChannelRevenueBreakdown("takeaway", 70m, 1),
             new OrderChannelRevenueBreakdown("delivery", 30m, 1)],
            result.Value.ByChannel);
        Assert.Equal(200m, result.Value.Revenue);
        Assert.Equal(3, result.Value.OrderCount);
        Assert.Equal(revenue.Value.Revenue, result.Value.ByChannel.Sum(channel => channel.Revenue));
        Assert.Equal(revenue.Value.OrderCount, result.Value.ByChannel.Sum(channel => channel.OrderCount));
        Assert.Equal(revenue.Value.StartUtc, result.Value.StartUtc);
        Assert.Equal(revenue.Value.EndUtcExclusive, result.Value.EndUtcExclusive);
        Assert.Equal(new DateOnly(2026, 10, 1), result.Value.FirstDate);
        Assert.Equal(new DateOnly(2026, 10, 31), result.Value.LastDateInclusive);
        Assert.Equal(BusinessTimeZone.Id, result.Value.TimeZoneId);
    }

    [Fact]
    public async Task GetChannels_AllocatesEarlierPaymentsBeforeFilteringTheBusinessDate()
    {
        var order = ConfirmedOrder("dine_in", 100m);
        Pay(order, 80m, PaymentMethod.Cash, AtUtc(2026, 9, 30, 16, 59, 59));
        Pay(order, 40m, PaymentMethod.Card, AtUtc(2026, 9, 30, 17));
        order.TransitionTo(OrderStatus.Completed, ActorId, AtUtc(2026, 10, 1, 1));

        var result = await new OrderChannelReportQueryHandler(new StubRepository(new RevenueOrderCandidate(order, false)))
            .GetChannelsAsync(OctoberQuery());

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(20m, result.Value.Revenue);
        Assert.Equal(1, result.Value.OrderCount);
        Assert.Equal(new OrderChannelRevenueBreakdown("dine_in", 20m, 1), result.Value.ByChannel[0]);
        Assert.Equal(new OrderChannelRevenueBreakdown("takeaway", 0m, 0), result.Value.ByChannel[1]);
        Assert.Equal(new OrderChannelRevenueBreakdown("delivery", 0m, 0), result.Value.ByChannel[2]);
    }

    [Fact]
    public async Task GetChannels_ReturnsAllThreeZeroBucketsForAnEmptyPeriod()
    {
        var result = await new OrderChannelReportQueryHandler(new StubRepository()).GetChannelsAsync(OctoberQuery());

        Assert.True(result.IsSuccess, result.Error?.Description);
        AssertZeroReport(result.Value);
    }

    [Theory]
    [InlineData(OrderStatus.Draft)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Preparing)]
    [InlineData(OrderStatus.Ready)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task GetChannels_ExcludesPaidOrdersThatAreNotCompleted(string status)
    {
        var repository = new StubRepository(new RevenueOrderCandidate(new PersistedOrderProjection(status: status), false));

        var result = await new OrderChannelReportQueryHandler(repository).GetChannelsAsync(OctoberQuery());

        Assert.True(result.IsSuccess, result.Error?.Description);
        AssertZeroReport(result.Value);
    }

    [Fact]
    public async Task GetChannels_ExcludesRestoredAndInvalidCompletedOrdersUsingTheRevenueRule()
    {
        var repository = new StubRepository(
            new(new PersistedOrderProjection(), true),
            new(new PersistedOrderProjection(paymentAmount: 90m), false),
            new(new PersistedOrderProjection(hasCompletion: false), false),
            new(new PersistedOrderProjection(hasCancellation: true), false),
            new(new PersistedOrderProjection(total: 0m), false),
            new(new PersistedOrderProjection(paymentStatus: PaymentStatus.Pending), false),
            new(new PersistedOrderProjection(paymentStatus: PaymentStatus.Failed), false),
            new(new PersistedOrderProjection(paymentHour: 4), false));

        var result = await new OrderChannelReportQueryHandler(repository).GetChannelsAsync(OctoberQuery());

        Assert.True(result.IsSuccess, result.Error?.Description);
        AssertZeroReport(result.Value);
    }

    [Fact]
    public async Task GetChannels_RejectsMissingTimeZoneWithoutReadingPersistence() =>
        await AssertValidationMatchesRevenue(
            OctoberQuery() with { BusinessTimeZone = null! }, "reports.time_zone_required");

    [Fact]
    public async Task GetChannels_RejectsReversedDatesWithoutReadingPersistence() =>
        await AssertValidationMatchesRevenue(
            OctoberQuery() with { FirstDate = new DateOnly(2026, 11, 1) }, "reports.invalid_date_range");

    [Fact]
    public async Task GetChannels_RejectsUnrepresentableEndBoundaryWithoutReadingPersistence() =>
        await AssertValidationMatchesRevenue(
            OctoberQuery() with { LastDateInclusive = DateOnly.MaxValue }, "reports.invalid_date_boundary");

    [Theory]
    [InlineData(2, 28)]
    [InlineData(3, 1)]
    public async Task GetChannels_RejectsSkippedMidnightAtEitherPeriodBoundary(int month, int day)
    {
        var date = new DateOnly(2026, month, day);
        await AssertValidationMatchesRevenue(new(date, date, MidnightTransitionZone()), "reports.invalid_date_boundary");
    }

    [Fact]
    public async Task GetChannels_UsesTheEarliestOccurrenceOfAnAmbiguousMidnight()
    {
        var timeZone = MidnightTransitionZone();
        var result = await new OrderChannelReportQueryHandler(new StubRepository()).GetChannelsAsync(new(
            new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1), timeZone));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(AtUtc(2026, 11, 1, 4).Value, result.Value.StartUtc);
        Assert.Equal(AtUtc(2026, 11, 2, 5).Value, result.Value.EndUtcExclusive);
    }

    private static async Task AssertValidationMatchesRevenue(OrderChannelReportQuery query, string expectedCode)
    {
        var repository = new StubRepository();
        var channels = await new OrderChannelReportQueryHandler(repository).GetChannelsAsync(query);
        var revenue = await new RevenueReportQueryHandler(repository).GetRevenueAsync(new(
            query.FirstDate, query.LastDateInclusive, query.BusinessTimeZone));

        Assert.True(channels.IsFailure);
        Assert.True(revenue.IsFailure);
        Assert.Equal(expectedCode, channels.Error?.Code);
        Assert.Equal(revenue.Error?.Code, channels.Error?.Code);
        Assert.Equal(revenue.Error?.Description, channels.Error?.Description);
        Assert.Equal(0, repository.CallCount);
    }

    private static void AssertZeroReport(OrderChannelReport report)
    {
        Assert.Equal(0m, report.Revenue);
        Assert.Equal(0, report.OrderCount);
        Assert.Equal(
            [new OrderChannelRevenueBreakdown("dine_in", 0m, 0),
             new OrderChannelRevenueBreakdown("takeaway", 0m, 0),
             new OrderChannelRevenueBreakdown("delivery", 0m, 0)],
            report.ByChannel);
    }

    private static OrderChannelReportQuery OctoberQuery() =>
        new(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), BusinessTimeZone);

    private static TimeZoneInfo MidnightTransitionZone() => TimeZoneInfo.CreateCustomTimeZone(
        "Midnight transitions", TimeSpan.FromHours(-5), "Midnight transitions", "Standard", "Daylight",
        [TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 11, 1))]);

    private static Order ConfirmedOrder(string channel, decimal price)
    {
        var createdAt = AtUtc(2026, 9, 30, 0);
        var category = new Category(EntityId.New(), "Coffee", createdAt, createdAt);
        var item = new MenuItem(EntityId.New(), category, "Latte", createdAt, createdAt);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Regular", new Money(price));
        var orderId = EntityId.New();
        var delivery = channel == "delivery"
            ? new Delivery(EntityId.New(), orderId, "Recipient", "0901234567", "123 Coffee Street")
            : null;
        var order = new Order(
            orderId, $"CHANNEL-{Guid.NewGuid():N}"[..24], channel, ActorId, createdAt,
            [new OrderLine(EntityId.New(), item, variant, 1)],
            tableId: channel == "dine_in" ? EntityId.New() : null, delivery: delivery);
        order.TransitionTo(OrderStatus.Confirmed, ActorId, AtUtc(2026, 9, 30, 1));
        return order;
    }

    private static void Pay(Order order, decimal amount, string method, UtcTimestamp paidAt)
    {
        var reference = method == PaymentMethod.Cash ? null : $"REF-{Guid.NewGuid():N}";
        order.RecordPayment(EntityId.New(), method, new Money(amount), PaymentStatus.Succeeded, paidAt, ActorId, reference);
        if (order.Status == OrderStatus.Confirmed)
        {
            order.TransitionTo(OrderStatus.Preparing, ActorId, paidAt);
            order.TransitionTo(OrderStatus.Ready, ActorId, paidAt);
        }
    }

    private static UtcTimestamp AtUtc(int year, int month, int day, int hour, int minute = 0, int second = 0) =>
        new(new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero));

    private sealed class StubRepository(params RevenueOrderCandidate[] candidates) : IRevenueReportRepository
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<RevenueOrderCandidate>> LoadCandidatesAsync(
            RevenuePeriod period, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<RevenueOrderCandidate>>(candidates);
        }
    }

    private sealed class PersistedOrderProjection : Order
    {
        public PersistedOrderProjection(
            string status = OrderStatus.Completed,
            decimal total = 100m,
            decimal paymentAmount = 100m,
            bool hasCompletion = true,
            bool hasCancellation = false,
            string paymentStatus = PaymentStatus.Succeeded,
            int paymentHour = 2)
        {
            Id = EntityId.New();
            Channel = "delivery";
            Total = new Money(total);
            RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(paymentAmount), paymentStatus,
                AtUtc(2026, 10, 15, paymentHour), ActorId);
            Status = status;
            CompletedAt = hasCompletion ? AtUtc(2026, 10, 15, 3) : null;
            CancelledAt = hasCancellation ? AtUtc(2026, 10, 15, 4) : null;
        }
    }
}
