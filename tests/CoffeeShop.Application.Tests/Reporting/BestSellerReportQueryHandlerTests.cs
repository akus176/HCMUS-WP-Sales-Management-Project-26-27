namespace CoffeeShop.Application.Tests.Reporting;

using CoffeeShop.Application.Reporting;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;

public sealed class BestSellerReportQueryHandlerTests
{
    private static readonly EntityId ActorId = EntityId.New();
    private static readonly TimeZoneInfo ShopZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    [Theory]
    [InlineData(BestSellerSort.Quantity, "Latte", "Espresso")]
    [InlineData(BestSellerSort.Revenue, "Espresso", "Latte")]
    public async Task RanksItemsAndVariantsByTheSelectedMetric(BestSellerSort sort, string first, string second)
    {
        var order = new Projection([
            new SnapshotLine(1, 10, 11, "Latte", "Small", 3, 30m),
            new SnapshotLine(2, 10, 12, "Latte", "Large", 1, 20m),
            new SnapshotLine(3, 20, 21, "Espresso", "Regular", 2, 80m)]);

        var result = await Handler(order).GetBestSellersAsync(Query() with { SortBy = sort });

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { first, second }, result.Value.Items.Select(row => row.ItemName));
        Assert.Equal(new long[] { 4, 2 }, result.Value.Items.OrderBy(row => row.MenuItemId!.Value).Select(row => row.Quantity));
        Assert.Equal(130m, result.Value.Items.Sum(row => row.Revenue));
        Assert.Equal(130m, result.Value.Variants.Sum(row => row.Revenue));
        Assert.Equal(sort == BestSellerSort.Quantity ? "Small" : "Regular", result.Value.Variants[0].SizeName);
        Assert.Equal(new[] { 1, 2, 3 }, result.Value.Variants.Select(row => row.Rank));
    }

    [Fact]
    public async Task AggregatesByIdentityAndUsesTheLatestSnapshotWithoutSplittingOnRenames()
    {
        var older = new Projection([new SnapshotLine(1, 10, 11, "Old Latte", "Small", 2, 20m)], day: 1);
        var newer = new Projection([new SnapshotLine(2, 10, 11, "Latte", "Regular", 1, 30m)], day: 2);

        var result = await Handler(older, newer).GetBestSellersAsync(Query());

        var item = Assert.Single(result.Value.Items);
        var variant = Assert.Single(result.Value.Variants);
        Assert.Equal("Latte", item.ItemName);
        Assert.Equal("Regular", variant.SizeName);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(50m, variant.Revenue);
    }

    [Fact]
    public async Task TiesAndLimitsAreIndependentOfRepositoryAndLineOrder()
    {
        var a = new Projection([new SnapshotLine(1, 10, 11, "Same", "Regular", 1, 10m)]);
        var b = new Projection([new SnapshotLine(2, 20, 21, "Same", "Regular", 1, 10m)]);
        var c = new Projection([new SnapshotLine(3, 30, 31, "Alpha", "Regular", 1, 10m)]);
        var query = Query() with { Limit = 2 };

        var first = (await Handler(a, b, c).GetBestSellersAsync(query)).Value;
        var reversed = (await Handler(c, b, a).GetBestSellersAsync(query)).Value;

        Assert.Equal(first.Items, reversed.Items);
        Assert.Equal(first.Variants, reversed.Variants);
        Assert.Equal(new[] { Id(30), Id(10) }, first.Items.Select(row => row.MenuItemId));
        Assert.Equal(new[] { 1, 2 }, first.Items.Select(row => row.Rank));
    }

    [Fact]
    public async Task AllocatesDiscountedCappedPaymentsProportionallyWithStableCentsAcrossAdjacentDates()
    {
        var order = new Projection([
            new SnapshotLine(3, 30, 31, "C", "Regular", 1, 1m),
            new SnapshotLine(2, 20, 21, "B", "Regular", 1, 1m),
            new SnapshotLine(1, 10, 11, "A", "Regular", 1, 1m)],
            total: 1m, payments: [(0.5m, At(1)), (1m, At(2))]);
        var repository = new StubRepository(new RevenueOrderCandidate(order, false));
        var handler = new BestSellerReportQueryHandler(repository);
        var full = (await handler.GetBestSellersAsync(Query())).Value;
        var first = (await handler.GetBestSellersAsync(Query() with { LastDateInclusive = new(2026, 10, 1) })).Value;
        var second = (await handler.GetBestSellersAsync(Query() with { FirstDate = new(2026, 10, 2) })).Value;
        var revenue = (await new RevenueReportQueryHandler(repository).GetRevenueAsync(new(
            new(2026, 10, 1), new(2026, 10, 31), ShopZone))).Value;

        Assert.Equal(new[] { 0.34m, 0.34m, 0.32m }, full.Items.OrderBy(row => row.ItemName).Select(row => row.Revenue));
        Assert.Equal(revenue.Revenue, full.Items.Sum(row => row.Revenue));
        Assert.Equal(0, first.Items.Sum(row => row.Quantity));
        Assert.Equal(3, second.Items.Sum(row => row.Quantity));
        foreach (var row in full.Items)
        {
            Assert.Equal(row.Revenue, first.Items.Single(item => item.MenuItemId == row.MenuItemId).Revenue
                + second.Items.Single(item => item.MenuItemId == row.MenuItemId).Revenue);
        }
    }

    [Fact]
    public async Task UsesSnapshotLineTotalsIncludingAddonsAndApprovedOrdinaryAndLoyaltyDiscounts()
    {
        var now = At(1);
        var category = new Category(EntityId.New(), "Coffee", now, now);
        var item = new MenuItem(Id(10), category, "Latte", now, now);
        var variant = new SizeVariant(Id(11), item.Id, "Regular", new Money(10m));
        var addon = new Addon(EntityId.New(), "Cream", new Money(5m));
        var line = new OrderLine(Id(1), item, variant, 2, [addon], [new MenuItemAddon(item.Id, addon.Id)]);
        var order = new Order(EntityId.New(), "BEST-ADDONS", "takeaway", ActorId, now,
            [line, new SnapshotLine(2, 20, 21, "Tea", "Regular", 1, 10m)]);
        var discount = order.RequestDiscount(EntityId.New(), DiscountType.Fixed, 10m, "Promotion", ActorId, now);
        order.ApproveDiscount(discount.Id, ActorId, now);
        order.ApplyLoyaltyRedemption(10, new Money(10m));
        order.RequestDiscount(EntityId.New(), DiscountType.Fixed, 5m, "Pending", ActorId, now);
        order.RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(50m), PaymentStatus.Succeeded, now, ActorId);
        foreach (var state in new[] { OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed })
        {
            order.TransitionTo(state, ActorId, now);
        }

        item.Update("Current name", null, category, now);
        variant.Update("Current size", new Money(100m));
        addon.Update("Current topping", new Money(50m));
        item.Deactivate(now);
        variant.Deactivate();
        var report = (await Handler(order).GetBestSellersAsync(Query())).Value;

        Assert.Equal(15m, report.Items.Single(row => row.ItemName == "Latte").Revenue);
        Assert.Equal(5m, report.Items.Single(row => row.ItemName == "Tea").Revenue);
        Assert.Equal("Regular", report.Variants[0].SizeName);
    }

    [Fact]
    public async Task CompletionWindowCountsPrepaidAndFreeOrdersWithoutRepeatingTheirQuantity()
    {
        var prepaid = new Projection([new SnapshotLine(1, 10, 11, "Prepaid", "Regular", 2, 10m)],
            payments: [(10m, new UtcTimestamp(new DateTimeOffset(2026, 9, 30, 16, 59, 59, TimeSpan.Zero)))]);
        var free = new Projection([new SnapshotLine(2, 20, 21, "Free", "Regular", 3, 0m)], total: 0m, payments: []);
        var report = (await Handler(prepaid, free).GetBestSellersAsync(Query())).Value;

        Assert.Equal(5, report.Items.Sum(row => row.Quantity));
        Assert.Equal(0m, report.Items.Sum(row => row.Revenue));
        Assert.Equal(new DateTimeOffset(2026, 9, 30, 17, 0, 0, TimeSpan.Zero), report.StartUtc);
        Assert.Equal(ShopZone.Id, report.TimeZoneId);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, false, false, 10)]
    [InlineData(OrderStatus.Ready, false, false, 10)]
    [InlineData(OrderStatus.Completed, true, false, 10)]
    [InlineData(OrderStatus.Completed, false, true, 10)]
    [InlineData(OrderStatus.Completed, false, false, 5)]
    public async Task ExcludesIneligibleOrders(string status, bool restored, bool cancelled, int paid)
    {
        var order = new Projection([new SnapshotLine(1, 10, 11, "Excluded", "Regular", 1, 10m)],
            status: status, cancelled: cancelled, payments: [(paid, At(1))]);
        var report = (await new BestSellerReportQueryHandler(new StubRepository(new RevenueOrderCandidate(order, restored)))
            .GetBestSellersAsync(Query())).Value;

        Assert.Empty(report.Items);
        Assert.Empty(report.Variants);
    }

    [Fact]
    public async Task NullCatalogReferencesUseSnapshotNamesWithoutMergingDifferentProductsOrSizes()
    {
        var order = new Projection([
            new SnapshotLine(1, null, null, "Latte", "Small", 1, 10m),
            new SnapshotLine(2, null, null, "Latte", "Large", 1, 20m),
            new SnapshotLine(3, null, null, "Tea", "Small", 1, 10m)]);
        var report = (await Handler(order).GetBestSellersAsync(Query())).Value;

        Assert.Equal(2, report.Items.Count);
        Assert.Equal(3, report.Variants.Count);
        Assert.Equal(2, report.Items.Single(row => row.ItemName == "Latte").Quantity);
    }

    [Fact]
    public async Task EmptyPeriodReturnsEmptyLists()
    {
        var report = (await new BestSellerReportQueryHandler(new StubRepository()).GetBestSellersAsync(Query())).Value;
        Assert.Empty(report.Items);
        Assert.Empty(report.Variants);
    }

    [Theory]
    [InlineData(0, 10, "reports.time_zone_required")]
    [InlineData(1, 10, "reports.invalid_date_range")]
    [InlineData(2, 10, "reports.invalid_date_boundary")]
    [InlineData(3, 0, "reports.invalid_ranking")]
    [InlineData(3, -1, "reports.invalid_ranking")]
    [InlineData(4, 10, "reports.invalid_ranking")]
    public async Task RejectsInvalidFiltersBeforeReadingPersistence(int scenario, int limit, string code)
    {
        var query = Query() with { Limit = limit };
        query = scenario switch
        {
            0 => query with { BusinessTimeZone = null! },
            1 => query with { FirstDate = new(2026, 11, 1) },
            2 => query with { LastDateInclusive = DateOnly.MaxValue },
            4 => query with { SortBy = (BestSellerSort)99 },
            _ => query,
        };
        var repository = new StubRepository();
        var result = await new BestSellerReportQueryHandler(repository).GetBestSellersAsync(query);
        Assert.True(result.IsFailure);
        Assert.Equal(code, result.Error?.Code);
        Assert.Equal(0, repository.CallCount);
    }

    private static BestSellerReportQuery Query() => new(new(2026, 10, 1), new(2026, 10, 31), ShopZone);
    private static UtcTimestamp At(int day) => new(new DateTimeOffset(2026, 10, day, 1, 0, 0, TimeSpan.Zero));
    private static EntityId Id(int value) => new(new Guid($"00000000-0000-0000-0000-{value:D12}"));
    private static BestSellerReportQueryHandler Handler(params Order[] orders) =>
        new(new StubRepository(orders.Select(order => new RevenueOrderCandidate(order, false)).ToArray()));

    private sealed class StubRepository(params RevenueOrderCandidate[] candidates) : IRevenueReportRepository
    {
        public int CallCount { get; private set; }
        public Task<IReadOnlyList<RevenueOrderCandidate>> LoadCandidatesAsync(RevenuePeriod period, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<RevenueOrderCandidate>>(candidates);
        }
    }

    private sealed class SnapshotLine : OrderLine
    {
        public SnapshotLine(int id, int? item, int? variant, string name, string size, int quantity, decimal total)
        {
            Id = BestSellerReportQueryHandlerTests.Id(id);
            MenuItemId = item is null ? null : BestSellerReportQueryHandlerTests.Id(item.Value);
            SizeVariantId = variant is null ? null : BestSellerReportQueryHandlerTests.Id(variant.Value);
            ItemNameSnapshot = name;
            SizeNameSnapshot = size;
            Quantity = quantity;
            LineTotal = new Money(total);
        }
    }

    private sealed class Projection : Order
    {
        public Projection(OrderLine[] lines, int day = 2, decimal? total = null,
            (decimal Amount, UtcTimestamp At)[]? payments = null,
            string status = OrderStatus.Completed, bool cancelled = false)
            : base(EntityId.New(), $"BEST-{Guid.NewGuid():N}"[..24], "takeaway", ActorId, At(1), lines)
        {
            Total = new Money(total ?? Subtotal.Amount);
            foreach (var payment in payments ?? [(Total.Amount, At(day))])
            {
                if (payment.Amount > 0)
                {
                    RecordPayment(EntityId.New(), PaymentMethod.Cash, new Money(payment.Amount), PaymentStatus.Succeeded, payment.At, ActorId);
                }
            }

            Status = status;
            CompletedAt = At(day);
            CancelledAt = cancelled ? At(day) : null;
        }
    }
}
