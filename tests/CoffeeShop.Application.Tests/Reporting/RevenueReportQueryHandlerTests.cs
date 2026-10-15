namespace CoffeeShop.Application.Tests.Reporting;

using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class RevenueReportQueryHandlerTests
{
    private static readonly EntityId ActorId = new(new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.CreateCustomTimeZone(
        "Coffee shop UTC+07", TimeSpan.FromHours(7), "Coffee shop UTC+07", "Coffee shop UTC+07");

    [Fact]
    public async Task GetRevenue_AggregatesTotalsDailyAndPaymentMethodsThroughTheSharedRule()
    {
        var split = CompletedOrder(100m,
            (60m, PaymentMethod.Cash, AtUtc(2026, 10, 1, 1)),
            (50m, PaymentMethod.Card, AtUtc(2026, 10, 2, 1)));
        var wallet = CompletedOrder(50m,
            (50m, PaymentMethod.EWallet, AtUtc(2026, 10, 2, 2)));
        var repository = new StubRepository(
            new RevenueOrderCandidate(split, InventoryWasRestored: false),
            new RevenueOrderCandidate(wallet, InventoryWasRestored: false));

        var result = await new RevenueReportQueryHandler(repository).GetRevenueAsync(
            Query(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2)));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(150m, result.Value.Revenue);
        Assert.Equal(2, result.Value.OrderCount);
        Assert.Equal(
            [new DailyRevenueBreakdown(new DateOnly(2026, 10, 1), 60m, 1),
             new DailyRevenueBreakdown(new DateOnly(2026, 10, 2), 90m, 2)],
            result.Value.Daily);
        Assert.Equal(
            [new PaymentMethodRevenueBreakdown(PaymentMethod.Cash, 60m, 1),
             new PaymentMethodRevenueBreakdown(PaymentMethod.Card, 40m, 1),
             new PaymentMethodRevenueBreakdown(PaymentMethod.EWallet, 50m, 1)],
            result.Value.ByPaymentMethod);
    }

    [Fact]
    public async Task GetRevenue_ExcludesRestoredOrdersThroughTheSharedRule()
    {
        var restored = CompletedOrder(100m,
            (100m, PaymentMethod.Cash, AtUtc(2026, 10, 1, 1)));
        var repository = new StubRepository(
            new RevenueOrderCandidate(restored, InventoryWasRestored: true));

        var result = await new RevenueReportQueryHandler(repository).GetRevenueAsync(
            Query(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(0m, result.Value.Revenue);
        Assert.Equal(0, result.Value.OrderCount);
        Assert.Empty(result.Value.Daily);
        Assert.Empty(result.Value.ByPaymentMethod);
    }

    [Fact]
    public async Task GetRevenue_ReturnsAnEmptyReportForNoCandidates()
    {
        var result = await new RevenueReportQueryHandler(new StubRepository()).GetRevenueAsync(
            Query(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));

        Assert.True(result.IsSuccess, result.Error?.Description);
        Assert.Equal(0m, result.Value.Revenue);
        Assert.Equal(0, result.Value.OrderCount);
        Assert.Empty(result.Value.Daily);
        Assert.Empty(result.Value.ByPaymentMethod);
    }

    [Fact]
    public async Task GetRevenue_RejectsAReversedDateRangeWithoutReadingPersistence()
    {
        var repository = new StubRepository();

        var result = await new RevenueReportQueryHandler(repository).GetRevenueAsync(
            Query(new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 31)));

        Assert.True(result.IsFailure);
        Assert.Equal("reports.invalid_date_range", result.Error?.Code);
        Assert.Equal(0, repository.CallCount);
    }

    [Fact]
    public async Task GetRevenue_RequiresABusinessTimeZone()
    {
        var repository = new StubRepository();
        var query = new RevenueReportQuery(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), null!);

        var result = await new RevenueReportQueryHandler(repository).GetRevenueAsync(query);

        Assert.True(result.IsFailure);
        Assert.Equal("reports.time_zone_required", result.Error?.Code);
        Assert.Equal(0, repository.CallCount);
    }

    private static RevenueReportQuery Query(DateOnly firstDate, DateOnly lastDate) =>
        new(firstDate, lastDate, BusinessTimeZone);

    private static Order CompletedOrder(
        decimal total,
        params (decimal Amount, string Method, UtcTimestamp PaidAt)[] payments)
    {
        var createdAt = AtUtc(2026, 9, 30, 0);
        var category = new Category(EntityId.New(), "Coffee", createdAt, createdAt);
        var item = new MenuItem(EntityId.New(), category, "Latte", createdAt, createdAt);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Regular", new Money(total));
        var order = new Order(
            EntityId.New(), $"REV-{Guid.NewGuid():N}"[..20], "takeaway", ActorId, createdAt,
            [new OrderLine(EntityId.New(), item, variant, 1)]);
        order.TransitionTo(OrderStatus.Confirmed, ActorId, AtUtc(2026, 9, 30, 1));
        order.TransitionTo(OrderStatus.Preparing, ActorId, AtUtc(2026, 9, 30, 2));
        order.TransitionTo(OrderStatus.Ready, ActorId, AtUtc(2026, 9, 30, 3));
        foreach (var payment in payments)
        {
            var reference = payment.Method == PaymentMethod.Cash ? null : $"REF-{Guid.NewGuid():N}";
            order.RecordPayment(
                EntityId.New(), payment.Method, new Money(payment.Amount), PaymentStatus.Succeeded,
                payment.PaidAt, ActorId, reference);
        }

        order.TransitionTo(
            OrderStatus.Completed,
            ActorId,
            new UtcTimestamp(payments.Max(payment => payment.PaidAt.Value).AddHours(1)));
        return order;
    }

    private static UtcTimestamp AtUtc(int year, int month, int day, int hour) =>
        new(new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.Zero));

    private sealed class StubRepository(params RevenueOrderCandidate[] candidates) : IRevenueReportRepository
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<RevenueOrderCandidate>> LoadCandidatesAsync(
            RevenuePeriod period,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<RevenueOrderCandidate>>(candidates);
        }
    }
}
