using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class PosWorkspaceTests
{
    [Fact]
    public void SelectingCustomer_PreservesItemsAndChannel_AndUsesOnlyAnActiveDefaultAddress()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), new()) { Channel = "delivery" };
        var size = new SizeVariantSummary(EntityId.New(), "Small", 25m, true);
        var item = new MenuItemSummary(EntityId.New(), EntityId.New(), "Coffee", "Latte", null, true, [size], []);
        model.AddLine(item, size, 2, []);
        var line = Assert.Single(model.Cart);
        var address = new CustomerAddressSummary(EntityId.New(), "2 Demo Street", "Home", true, true);
        var customer = Customer() with { Addresses = [new(EntityId.New(), "Old address", "Old", true, false), address] };

        model.SelectCustomer(customer);

        Assert.Equal(line, Assert.Single(model.Cart));
        Assert.Equal("delivery", model.Channel);
        Assert.Equal(customer.Id, model.Customer.CustomerId);
        Assert.Equal(customer.Phone, model.Customer.ContactNumber);
        Assert.Equal(customer.FullName, model.CustomerLabel);
        Assert.Equal(address.Id, model.Customer.CustomerAddressId);
        Assert.Equal(address.AddressLine, model.Customer.AddressLine);
        Assert.Equal(120, model.CustomerProfile!.LoyaltyPointsBalance);

        model.SelectCustomer(Customer());
        Assert.Null(model.Customer.CustomerAddressId);
        Assert.Null(model.Customer.AddressLine);
        Assert.Equal(line, Assert.Single(model.Cart));
    }

    [Fact]
    public async Task UnavailableCustomer_AndUncertainTicket_KeepExistingSelection()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), new());
        var current = Customer();
        model.SelectCustomer(current);
        model.SelectCustomer(Customer() with { IsActive = false });
        Assert.Equal(current.Id, model.Customer.CustomerId);
        Assert.NotNull(model.Error);

        await model.LoadAsync(); // Missing services simulate a ticket requiring reload.
        Assert.True(model.RequiresReload);
        model.SelectCustomer(Customer());
        Assert.Equal(current.Id, model.Customer.CustomerId);
        model.RefreshCustomerProfile(Customer());
        Assert.Equal(current.Id, model.CustomerProfile!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WalkInAndNewTicket_ClearSharedSelection_SoNavigationCannotRestoreAnOldCustomer(bool newTicket)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new CustomerOrderContext();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), context, new());
        var customer = Customer();
        context.Set(new(customer.Id, true, RecipientName: customer.FullName, ContactNumber: customer.Phone),
            new(customer.Id, customer.FullName, null, "2 Demo Street"));
        model.UseCustomerSelection();
        Assert.Equal("delivery", model.Channel);
        model.RefreshCustomerProfile(customer);

        if (newTicket) model.NewTicket();
        else model.WalkIn();
        model.UseCustomerSelection();

        Assert.Null(context.Request.CustomerId);
        Assert.Null(model.Customer.CustomerId);
        Assert.Null(model.CustomerProfile);
        Assert.Equal("Walk-in", model.CustomerLabel);
        Assert.Equal("takeaway", model.Channel);
    }

    private static CustomerDetail Customer() => new(EntityId.New(), "Binh Tran", "+84090080002", null, true, 120,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, [], []);

    [Fact]
    public void IdenticalItems_HaveIndependentIdentity_AndStaleReferencesTargetTheSameLine()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), new());
        var size = new SizeVariantSummary(EntityId.New(), "Small", 25m, true);
        var item = new MenuItemSummary(EntityId.New(), EntityId.New(), "Coffee", "Latte", null, true, [size], []);
        model.AddLine(item, size, 1, []);
        model.AddLine(item, size, 1, []);
        var first = model.Cart[0];
        var second = model.Cart[1];
        Assert.NotEqual(first.LineId, second.LineId);

        model.ChangeQuantity(second, 3);
        model.ChangeQuantity(second, 4);
        Assert.Equal(1, model.Cart[0].Quantity);
        Assert.Equal(4, model.Cart[1].Quantity);
        Assert.Equal(second.LineId, model.Cart[1].LineId);
        Assert.Equal("100.00", model.Cart[1].Amount);

        model.RemoveLine(second);
        Assert.Equal(first.LineId, Assert.Single(model.Cart).LineId);
        model.ChangeQuantity(second, 2);
        model.RemoveLine(second);
        Assert.Equal(first.LineId, Assert.Single(model.Cart).LineId);
    }

    [Fact]
    public void RemovingALine_ClearsObsoleteValidation_AndKeepsOtherItems()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), new());
        var size = new SizeVariantSummary(EntityId.New(), "Small", 25m, true);
        var item = new MenuItemSummary(EntityId.New(), EntityId.New(), "Coffee", "Latte", null, true, [size], []);
        model.AddLine(item, size, 1, []);
        model.AddLine(item, size, 2, []);
        model.ShowValidation("Choose an operator.");
        model.RemoveLine(model.Cart[0]);
        Assert.Null(model.Error);
        Assert.Equal(2, Assert.Single(model.Cart).Quantity);
        Assert.Contains("removed", model.Message);
    }

    [Fact]
    public void InvalidQuantityAndDuplicateAddons_KeepCartUnchanged()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), new());
        var size = new SizeVariantSummary(EntityId.New(), "Small", 25m, true);
        var addon = new AddonSummary(EntityId.New(), "Cream", 5m, true);
        var item = new MenuItemSummary(EntityId.New(), EntityId.New(), "Coffee", "Latte", null, true, [size], [addon]);
        model.AddLine(item, size, 0, []);
        Assert.NotNull(model.Error);
        Assert.Empty(model.Cart);
        model.AddLine(item, size, 1, [addon, addon]);
        Assert.NotNull(model.Error);
        Assert.Empty(model.Cart);
        model.AddLine(item, size, 2, [addon]);
        Assert.Null(model.Error);
        Assert.Contains("2 x Latte / Small @ 25.00", Assert.Single(model.Cart).Description);
    }

    [Fact]
    public void NewTicket_ClearsOnlyLocalSelection_AndChangesIdentity()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), new());
        var id = model.TicketId;
        model.NewTicket();
        Assert.NotEqual(id, model.TicketId);
        Assert.True(model.CanEdit);
        Assert.False(model.CanConfirm);
    }

    [Fact]
    public void QuantityEditing_UpdatesDraftLinesAndNewTicketClearsCategory()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), new());
        var size = new SizeVariantSummary(EntityId.New(), "Small", 25m, true);
        var categoryId = EntityId.New();
        var item = new MenuItemSummary(EntityId.New(), categoryId, "Coffee", "Latte", null, true, [size], []);
        model.SelectCategory(categoryId);
        model.AddLine(item, size, 1, []);

        var line = Assert.Single(model.Cart);
        model.ChangeQuantity(line, 3);
        Assert.Equal(3, Assert.Single(model.Cart).Quantity);
        model.ChangeQuantity(Assert.Single(model.Cart), 0);
        Assert.Empty(model.Cart);

        model.NewTicket();
        Assert.Null(model.CategoryId);
    }

    [Fact]
    public async Task UnexpectedFailure_RequiresReloadAndPreservesTicketIdentity()
    {
        // A missing service simulates an unexpected adapter/composition failure.
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new PosWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new(), new());
        var id = model.TicketId;
        await model.LoadAsync();
        Assert.Equal("pos.database_error", model.Error!.Code);
        Assert.Equal(WorkflowFeedbackKind.DatabaseError, model.Feedback.Kind);
        Assert.True(model.RequiresReload);
        Assert.False(model.IsBusy);
        Assert.False(model.CanEdit);
        Assert.False(model.CanConfirm);
        model.NewTicket();
        Assert.Equal(id, model.TicketId);
        Assert.Contains("write may have succeeded", model.Message);
    }
}
