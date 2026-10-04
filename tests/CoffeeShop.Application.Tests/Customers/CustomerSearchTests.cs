namespace CoffeeShop.Application.Tests.Customers;

using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;

public class CustomerSearchTests
{
    private static readonly UtcTimestamp T0 = new(new DateTimeOffset(2026, 10, 4, 1, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp T1 = new(new DateTimeOffset(2026, 10, 4, 2, 0, 0, TimeSpan.Zero));
    private static readonly UtcTimestamp T2 = new(new DateTimeOffset(2026, 10, 4, 3, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Search_WithNullOrEmptyQuery_ReturnsAllActiveCustomersDeterministicallyOrdered()
    {
        var c1 = new Customer(EntityId.New(), "Charlie Brown", "0903333333", T0, T0);
        var c2 = new Customer(EntityId.New(), "Alice Smith", "0901111111", T1, T1);
        var c3 = new Customer(EntityId.New(), "Bob Taylor", "0902222222", T2, T2);
        var inactive = new Customer(EntityId.New(), "Aaron Inactive", "0900000000", T0, T0, isActive: false);

        var results = CustomerSearch.Search([c1, c2, c3, inactive], new CustomerSearchCriteria());

        Assert.Equal(3, results.Count);
        Assert.Equal("Alice Smith", results[0].FullName);
        Assert.Equal("Bob Taylor", results[1].FullName);
        Assert.Equal("Charlie Brown", results[2].FullName);
    }

    [Fact]
    public void Search_WithIncludeInactiveFalse_ExcludesInactiveCustomers()
    {
        var active = new Customer(EntityId.New(), "David Active", "0901234567", T0, T0, isActive: true);
        var inactive = new Customer(EntityId.New(), "David Inactive", "0901234568", T0, T0, isActive: false);

        var results = CustomerSearch.Search([active, inactive], new CustomerSearchCriteria(Query: "David", IncludeInactive: false));

        var single = Assert.Single(results);
        Assert.Equal("David Active", single.FullName);
    }

    [Fact]
    public void Search_WithIncludeInactiveTrue_IncludesInactiveCustomers()
    {
        var active = new Customer(EntityId.New(), "David Active", "0901234567", T0, T0, isActive: true);
        var inactive = new Customer(EntityId.New(), "David Inactive", "0901234568", T0, T0, isActive: false);

        var results = CustomerSearch.Search([active, inactive], new CustomerSearchCriteria(Query: "David", IncludeInactive: true));

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_WithExactPhoneMatch_RanksHigherThanPartialMatch()
    {
        var targetPhone = "0901234567";
        var exactCustomer = new Customer(EntityId.New(), "Zachary Taylor", targetPhone, T0, T0);
        var prefixCustomer = new Customer(EntityId.New(), "Alice Adams", "09012345678", T0, T0);
        var infixCustomer = new Customer(EntityId.New(), "Bob Baker", "109012345679", T0, T0);

        var results = CustomerSearch.Search([infixCustomer, prefixCustomer, exactCustomer], new CustomerSearchCriteria(Query: targetPhone));

        Assert.Equal(3, results.Count);
        Assert.Equal(exactCustomer.Id, results[0].Id);
        Assert.Equal(prefixCustomer.Id, results[1].Id);
        Assert.Equal(infixCustomer.Id, results[2].Id);
    }

    [Fact]
    public void Search_WithNameMatch_RanksPrefixBeforeInfixMatch()
    {
        var prefixMatch = new Customer(EntityId.New(), "Alexander Great", "0901111111", T0, T0);
        var infixMatch = new Customer(EntityId.New(), "Sir Alexander", "0902222222", T0, T0);

        var results = CustomerSearch.Search([infixMatch, prefixMatch], new CustomerSearchCriteria(Query: "Alex"));

        Assert.Equal(2, results.Count);
        Assert.Equal("Alexander Great", results[0].FullName);
        Assert.Equal("Sir Alexander", results[1].FullName);
    }

    [Fact]
    public void Search_WithEmailMatch_FindsCustomer()
    {
        var customer = new Customer(EntityId.New(), "John Doe", "0905555555", T0, T0, email: "john.special@example.com");

        var results = CustomerSearch.Search([customer], new CustomerSearchCriteria(Query: "special"));

        var matched = Assert.Single(results);
        Assert.Equal("John Doe", matched.FullName);
    }

    [Fact]
    public void Search_WithLimit_RestrictsReturnedResultCount()
    {
        var c1 = new Customer(EntityId.New(), "A1", "0901111111", T0, T0);
        var c2 = new Customer(EntityId.New(), "A2", "0902222222", T0, T0);
        var c3 = new Customer(EntityId.New(), "A3", "0903333333", T0, T0);

        var results = CustomerSearch.Search([c1, c2, c3], new CustomerSearchCriteria(Query: "A", Limit: 2));

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_WithNoMatches_ReturnsEmptyList()
    {
        var c1 = new Customer(EntityId.New(), "A1", "0901111111", T0, T0);

        var results = CustomerSearch.Search([c1], new CustomerSearchCriteria(Query: "non-existent-query"));

        Assert.Empty(results);
    }

    [Fact]
    public void Search_DeterministicTieBreaking_OrdersByFullNameThenPhoneThenCreatedAt()
    {
        var c1 = new Customer(EntityId.New(), "Smith John", "0902222222", T1, T1);
        var c2 = new Customer(EntityId.New(), "Smith Adam", "0901111111", T0, T0);
        var c3 = new Customer(EntityId.New(), "Smith Adam", "0903333333", T2, T2);

        var results = CustomerSearch.Search([c1, c2, c3], new CustomerSearchCriteria(Query: "Smith"));

        Assert.Equal(3, results.Count);
        // "Smith Adam" (phone 0901111111) before "Smith Adam" (phone 0903333333) before "Smith John"
        Assert.Equal("0901111111", results[0].Phone);
        Assert.Equal("0903333333", results[1].Phone);
        Assert.Equal("Smith John", results[2].FullName);
    }

    [Fact]
    public void WalkInOrder_WithoutCustomerProfile_CanProceedSuccessfully()
    {
        // Demonstrates that customer search can yield no customer for a walk-in,
        // and order draft creation proceeds with CustomerId = null as required by OpenSpec task T5.1.
        var searchResults = CustomerSearch.Search([], new CustomerSearchCriteria(Query: "walk-in"));
        Assert.Empty(searchResults);

        var category = new Category(EntityId.New(), "Coffee", T0, T0);
        var item = new MenuItem(EntityId.New(), category, "Espresso", T0, T0);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Standard", new Money(30000m));
        var orderId = EntityId.New();

        var request = new CreateOrderDraftRequest(
            Id: orderId,
            OrderNumber: "ORD-WALKIN-1",
            Channel: "takeaway",
            CreatedBy: EntityId.New(),
            CreatedAt: T0,
            Lines: [new OrderLineSelection(item, variant, 1, [])],
            CustomerId: null);

        var orderResult = new OrderDraftFactory().Create(request);

        Assert.True(orderResult.IsSuccess);
        Assert.Null(orderResult.Value.CustomerId);
    }
}
