namespace CoffeeShop.Application.Tests.Customers;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;

public sealed class CustomerQueryHandlerTests
{
    [Fact]
    public async Task Search_NormalizesCriteriaAndReturnsUiReadModels()
    {
        var repository = new FakeCustomerRepository();
        var summary = new CustomerSummary(EntityId.New(), "Alice", "0901234567", null, true, 25);
        repository.SearchResults = [summary];
        var handler = new CustomerQueryHandler(repository);

        var result = await handler.SearchCustomersAsync(new(" alice ", IncludeInactive: true, Limit: 25));

        Assert.True(result.IsSuccess);
        Assert.Same(summary, Assert.Single(result.Value));
        Assert.Equal("alice", repository.LastSearchCriteria!.Query);
        Assert.True(repository.LastSearchCriteria.IncludeInactive);
        Assert.Equal(25, repository.LastSearchCriteria.Limit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public async Task Search_WithInvalidLimit_ReturnsValidation(int limit)
    {
        var repository = new FakeCustomerRepository();
        var handler = new CustomerQueryHandler(repository);

        var result = await handler.SearchCustomersAsync(new(Limit: limit));

        AssertError(result, ErrorType.Validation, "customers.invalid_limit");
        Assert.Null(repository.LastSearchCriteria);
    }

    [Fact]
    public async Task DetailAndHistory_ReturnReadModels()
    {
        var repository = new FakeCustomerRepository();
        var id = EntityId.New();
        var now = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        repository.DetailResult = new(
            id, "Alice", "0901234567", null, true, 10, now, now,
            [new(EntityId.New(), "1 Main Street", "Home", true, true)],
            [new(EntityId.New(), "ORD-1", "completed", 100m, now)]);
        repository.HistoryResult = new(
            [new(EntityId.New(), "earn", 10, 10, EntityId.New(), null, now, EntityId.New())],
            0, 20, 1);
        var handler = new CustomerQueryHandler(repository);

        var detail = await handler.GetCustomerDetailAsync(id);
        var history = await handler.GetLoyaltyHistoryAsync(id, new(0, 20));

        Assert.Equal("Alice", detail.Value.FullName);
        Assert.Single(detail.Value.Addresses);
        Assert.Single(detail.Value.RecentOrders);
        Assert.Equal(10, Assert.Single(history.Value.Items).Points);
        Assert.Equal(20, repository.LastHistoryCriteria!.Limit);
    }

    [Fact]
    public async Task MissingCustomerAndSettings_ReturnNotFound()
    {
        var handler = new CustomerQueryHandler(new FakeCustomerRepository());

        AssertError(await handler.GetCustomerDetailAsync(EntityId.New()),
            ErrorType.NotFound, "customers.customer_not_found");
        AssertError(await handler.GetLoyaltyHistoryAsync(EntityId.New()),
            ErrorType.NotFound, "customers.customer_not_found");
        AssertError(await handler.GetLoyaltySettingsAsync(),
            ErrorType.NotFound, "loyalty.settings_not_found");
    }

    [Fact]
    public async Task Settings_ReturnConfiguredValues()
    {
        var repository = new FakeCustomerRepository();
        repository.SettingsResult = new(
            EntityId.New(), 0.1m, 100m, 20m, DateTimeOffset.UtcNow, EntityId.New());
        var handler = new CustomerQueryHandler(repository);

        var result = await handler.GetLoyaltySettingsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, result.Value.RedeemValuePerPoint);
    }

    private static void AssertError(Result result, ErrorType type, string code)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(type, result.Error!.Type);
        Assert.Equal(code, result.Error.Code);
    }
}
