namespace CoffeeShop.WinUI.Tests;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Common.Time;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Sales;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

public sealed class CustomerWorkspaceTests
{
    [Fact]
    public async Task Operator_ManagesProfileAddressesAndOrderSelectionWithoutLosingHistory()
    {
        using var setup = new Setup();
        var model = setup.Model;
        await model.LoadAsync();
        Assert.True(model.IsEmpty);
        Assert.Null(model.OrderSelection.CustomerId);

        Assert.True(await model.CreateAsync(new(" Alice ", "0901234567", "ALICE@EXAMPLE.COM")));
        var id = model.SelectedCustomer!.Id;
        Assert.Equal("alice@example.com", model.SelectedCustomer.Email);
        Assert.True(await model.UpdateAsync(new(id, "Alice Nguyen", "0901234567")));
        Assert.True(await model.AddAddressAsync(new(id, "1 Main Street", "Home", true)));
        var address = Assert.Single(model.SelectedCustomer!.Addresses);
        Assert.True(await model.AddAddressAsync(new(id, "2 Main Street", "Office")));
        Assert.True(await model.SetDefaultAddressAsync(id, model.SelectedCustomer!.Addresses[1].Id));
        Assert.True(await model.UpdateAddressAsync(new(id, address.Id, "3 Main Street", "Home", false)));

        Assert.True(await model.SetOrderSelectionAsync(new(id, true, address.Id, "Alice", "0901234567")));
        Assert.Equal("3 Main Street", model.OrderSelection.AddressLine);
        Assert.True(await model.DeactivateAddressAsync(id, address.Id));
        Assert.Null(model.OrderSelection.CustomerId);

        Assert.True(await model.SetOrderSelectionAsync(new(id)));
        var anotherView = new CustomerWorkspace(setup.Scopes, setup.Context);
        Assert.Equal(id, anotherView.OrderSelection.CustomerId);
        Assert.True(await model.DeactivateAsync(id));
        Assert.Null(model.OrderSelection.CustomerId);
        Assert.Empty(model.Customers);
        Assert.False(model.SelectedCustomer!.IsActive);
        Assert.False(model.CanEdit);
        model.IncludeInactive = true;
        model.Search = "nguyen";
        await model.LoadAsync();
        Assert.Equal(id, Assert.Single(model.Customers).Id);
        Assert.Equal(2, model.SelectedCustomer.Addresses.Count);
    }

    [Fact]
    public async Task ValidationAndConflict_StayVisibleAndLeaveSelectionUnchanged()
    {
        using var setup = new Setup();
        var model = setup.Model;
        Assert.False(await model.CreateAsync(new("", "bad")));
        Assert.Equal(ErrorType.Validation, model.Error!.Type);
        Assert.NotEmpty(model.Error.Details);
        Assert.True(await model.CreateAsync(new("Alice", "0901234567")));
        var id = model.SelectedCustomer!.Id;
        Assert.True(await model.SetOrderSelectionAsync(new(id)));
        Assert.False(await model.CreateAsync(new("Another", "0901234567")));
        Assert.Equal(ErrorType.Conflict, model.Error!.Type);
        Assert.False(await model.SetOrderSelectionAsync(new(id, true, EntityId.New(), "Alice", "0901234567")));
        Assert.Equal(ErrorType.Conflict, model.Error!.Type);
        Assert.Equal(id, model.OrderSelection.CustomerId);
        Assert.Null(model.OrderSelection.Delivery);
    }

    [Fact]
    public async Task WalkInDelivery_IsOrderOnlyAndCanBeCleared()
    {
        using var setup = new Setup();
        Assert.True(await setup.Model.SetOrderSelectionAsync(new(IsDelivery: true,
            RecipientName: "Guest", ContactNumber: "0901234567", AddressLine: "10 Main Street")));
        Assert.Null(setup.Model.OrderSelection.CustomerId);
        Assert.True(setup.Model.OrderSelection.Delivery!.IsOrderSpecific);
        Assert.Empty(setup.Store.Customers);
        setup.Model.ContinueAsWalkIn();
        Assert.Null(setup.Model.OrderSelection.Delivery);
    }

    [Fact]
    public async Task DatabaseFailure_RequiresReloadAndDoesNotExposeExceptionDetails()
    {
        using var setup = new Setup();
        setup.Reads.Fail = true;
        await setup.Model.LoadAsync();
        Assert.True(setup.Model.IsError);
        Assert.True(setup.Model.RequiresReload);
        Assert.False(setup.Model.IsBusy);
        Assert.DoesNotContain("private database details", setup.Model.Message);
        Assert.False(await setup.Model.CreateAsync(new("Alice", "0901234567")));
        Assert.Empty(setup.Store.Customers);
        setup.Reads.Fail = false;
        await setup.Model.LoadAsync();
        Assert.False(setup.Model.RequiresReload);
        Assert.False(setup.Model.IsError);
        Assert.True(setup.Model.IsEmpty);
    }

    [Fact]
    public async Task SuccessfulSaveWithFailedRefresh_DoesNotInviteDuplicateCreation()
    {
        using var setup = new Setup();
        setup.Reads.Fail = true;
        Assert.True(await setup.Model.CreateAsync(new("Alice", "0901234567")));
        Assert.Single(setup.Store.Customers);
        Assert.Contains("Customer created. Refresh failed", setup.Model.Message);
        Assert.True(setup.Model.RequiresReload);
        Assert.False(await setup.Model.CreateAsync(new("Bob", "0907654321")));
        Assert.Single(setup.Store.Customers);
        setup.Reads.Fail = false;
        await setup.Model.LoadAsync();
        Assert.Equal("Alice", setup.Model.SelectedCustomer!.FullName);
        Assert.False(setup.Model.RequiresReload);
    }

    [Fact]
    public async Task Loading_BlocksConcurrentOperationsAndFinishesCleanly()
    {
        using var setup = new Setup();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        setup.Reads.Gate = gate.Task;
        var load = setup.Model.LoadAsync();
        Assert.True(setup.Model.IsBusy);
        Assert.False(await setup.Model.CreateAsync(new("Alice", "0901234567")));
        Assert.Empty(setup.Store.Customers);
        gate.SetResult();
        await load;
        Assert.False(setup.Model.IsBusy);
        Assert.True(setup.Model.IsEmpty);
    }

    [Fact]
    public async Task CompletedPaidOrder_ChangesVisibleBalanceOnceAndHistoryCanBePaged()
    {
        using var setup = new Setup();
        Assert.True(await setup.Model.CreateAsync(new("Alice", "0901234567")));
        var customer = setup.Store.Customers.Values.Single();
        var now = new TestClock().UtcNow;
        setup.Store.Settings = new(EntityId.New(), 0.1m, 1m, 50m, now);
        for (var index = 0; index < 21; index++)
        {
            customer.EarnLoyaltyPoints(EntityId.New(), 1, now, EntityId.New());
        }

        await setup.Model.LoadAsync();
        Assert.Equal(21, setup.Model.SelectedCustomer!.LoyaltyPointsBalance);
        Assert.Equal(20, setup.Model.History.Items.Count);
        Assert.True(setup.Model.CanNext);
        await setup.Model.NextHistoryAsync();
        Assert.Single(setup.Model.History.Items);
        Assert.True(setup.Model.CanPrevious);
        Assert.False(setup.Model.CanNext);

        var order = new PaidCompletedOrder(customer.Id);
        setup.Store.Orders.Add(order.Id, order);
        var loyalty = new LoyaltyCommandHandler(setup.Store, new FakeUnitOfWork(),
            new TestTransactionManager(), new TestClock(), TestCurrentUser.Authenticated());
        Assert.False((await loyalty.EarnForCompletedOrderAsync(new(order.Id))).Value.AlreadyApplied);
        await setup.Model.RefreshAfterOrderCompletionAsync(customer.Id);
        Assert.Equal(31, setup.Model.SelectedCustomer.LoyaltyPointsBalance);
        Assert.Equal(0, setup.Model.History.Offset);
        Assert.Equal(22, setup.Model.History.TotalCount);

        Assert.True((await loyalty.EarnForCompletedOrderAsync(new(order.Id))).Value.AlreadyApplied);
        await setup.Model.RefreshAfterOrderCompletionAsync(customer.Id);
        Assert.Equal(31, setup.Model.SelectedCustomer.LoyaltyPointsBalance);
        Assert.Equal(22, setup.Model.History.TotalCount);
        Assert.Single(customer.LoyaltyTransactions, transaction => transaction.OrderId == order.Id);
    }

    private sealed class PaidCompletedOrder : Order
    {
        public PaidCompletedOrder(EntityId customerId)
        {
            Id = EntityId.New();
            CustomerId = customerId;
            Status = OrderStatus.Completed;
            Total = new Money(100m);
            PaidAmount = new Money(100m);
        }
    }

    private sealed class ReadState
    {
        public bool Fail { get; set; }
        public Task? Gate { get; set; }
    }

    private sealed class ReadRepository(FakeCustomerRepository store, ReadState state) : ICustomerReadRepository
    {
        public async Task<IReadOnlyList<CustomerSummary>> SearchCustomersAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            if (state.Gate is not null)
            {
                await state.Gate;
            }

            if (state.Fail)
            {
                throw new InvalidOperationException("private database details");
            }

            return CustomerSearch.Search(store.Customers.Values, criteria)
                .Select(customer => new CustomerSummary(customer.Id, customer.FullName, customer.Phone, customer.Email, customer.IsActive, customer.LoyaltyPointsBalance)).ToArray();
        }

        public Task<CustomerDetail?> GetCustomerDetailAsync(EntityId customerId, CancellationToken cancellationToken = default)
        {
            var customer = store.Customers.GetValueOrDefault(customerId);
            return Task.FromResult(customer is null ? null : new CustomerDetail(
                customer.Id, customer.FullName, customer.Phone, customer.Email, customer.IsActive, customer.LoyaltyPointsBalance,
                customer.CreatedAt.Value, customer.UpdatedAt.Value,
                customer.Addresses.Select(address => new CustomerAddressSummary(address.Id, address.AddressLine, address.Label, address.IsDefault, address.IsActive)).ToArray(), []));
        }

        public Task<LoyaltyHistoryPage?> GetLoyaltyHistoryAsync(EntityId customerId, LoyaltyHistoryCriteria criteria, CancellationToken cancellationToken = default)
        {
            var customer = store.Customers.GetValueOrDefault(customerId);
            var history = customer?.LoyaltyTransactions.OrderByDescending(item => item.CreatedAt.Value).ThenByDescending(item => item.Id.Value).ToArray() ?? [];
            return Task.FromResult(customer is null ? null : new LoyaltyHistoryPage(
                history.Skip(criteria.Offset).Take(criteria.Limit).Select(item => new LoyaltyTransactionSummary(
                    item.Id, item.Type, item.Points, item.BalanceAfter, item.OrderId, item.ReversesTransactionId, item.CreatedAt.Value, item.CreatedBy)).ToArray(),
                criteria.Offset, criteria.Limit, history.Length));
        }

        public Task<LoyaltySettingsDetail?> GetLoyaltySettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult<LoyaltySettingsDetail?>(null);
    }

    private sealed class Setup : IDisposable
    {
        private readonly ServiceProvider provider;
        public FakeCustomerRepository Store { get; } = new();
        public ReadState Reads { get; } = new();
        public CustomerOrderContext Context { get; } = new();
        public IServiceScopeFactory Scopes => provider.GetRequiredService<IServiceScopeFactory>();
        public CustomerWorkspace Model { get; }

        public Setup()
        {
            provider = new ServiceCollection()
                .AddSingleton<ICustomerRepository>(Store)
                .AddScoped<ICustomerReadRepository>(_ => new ReadRepository(Store, Reads))
                .AddSingleton<IUnitOfWork>(new FakeUnitOfWork())
                .AddSingleton<IClock>(new TestClock())
                .AddScoped<CustomerCommandHandler>()
                .AddScoped<CustomerQueryHandler>()
                .AddScoped<CustomerOrderSelectionHandler>()
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
            Model = new CustomerWorkspace(Scopes, Context);
        }

        public void Dispose() => provider.Dispose();
    }
}
