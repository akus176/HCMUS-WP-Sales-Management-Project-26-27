using System.ComponentModel;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.ViewModels;

public sealed record PosCartLine(MenuItemSummary Item, SizeVariantSummary Size, int Quantity, IReadOnlyList<AddonSummary> Addons)
{
    public string Description => $"{Quantity} x {Item.Name} / {Size.SizeName} @ {Size.Price:0.00}"
        + string.Concat(Addons.Select(addon => $"\n+ {addon.Name} @ {addon.Price:0.00} per drink"));
    public PosLineRequest Request => new(Item.Id, Size.Id, Quantity, Addons.Select(addon => addon.Id).ToArray());
}

public sealed class PosWorkspace(IServiceScopeFactory scopes, CustomerOrderContext customerContext, PosOperatorSession session) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Search { get; set; } = "";
    public string Channel { get; set; } = "takeaway";
    public EntityId? TableId { get; set; }
    public CustomerOrderSelectionRequest Customer { get; set; } = new();
    public string CustomerLabel { get; private set; } = "Walk-in";
    public PosOperatorSession Session => session;
    public EntityId TicketId { get; private set; } = EntityId.New();
    public IReadOnlyList<MenuItemSummary> Menu { get; private set; } = [];
    public PosOptions Options { get; private set; } = new([], []);
    public IReadOnlyList<PosCartLine> Cart { get; private set; } = [];
    public PosOrderDetail? Order { get; private set; }
    public bool IsBusy { get; private set; }
    public bool RequiresReload { get; private set; }
    public Error? Error { get; private set; }
    public string Message { get; private set; } = "Load the menu and choose an operator to start a ticket.";
    public bool CanEdit => !IsBusy && !RequiresReload && Order is null;
    public bool CanConfirm => !IsBusy && !RequiresReload && Order?.Status == OrderStatus.Draft;
    public bool IsEmpty => !IsBusy && Error is null && Menu.Count == 0;
    public WorkflowFeedback Feedback => WorkflowFeedback.Resolve(IsBusy, IsEmpty, Error, Message);

    public Task LoadAsync() => RunAsync(async provider =>
    {
        var queries = provider.GetRequiredService<CatalogQueryHandler>();
        var menu = await queries.SearchMenuItemsAsync(new(Search: Search, IsActive: true, Limit: 200));
        var categories = await queries.SearchCategoriesAsync(new(IsActive: true, Limit: 200));
        if (!Accept(menu) || !Accept(categories)) return;
        var activeCategories = categories.Value.Select(category => category.Id).ToHashSet();
        Menu = menu.Value.Where(item => activeCategories.Contains(item.CategoryId) && item.SizeVariants.Any(size => size.IsActive)).ToArray();
        var handler = provider.GetRequiredService<PosOrderHandler>();
        Options = await handler.GetOptionsAsync();
        if (session.Operator is { } actor && Options.Staff.All(staff => staff.Id != actor.Id)) session.Operator = null;
        if (Order is not null || RequiresReload)
        {
            var result = await handler.GetOrderAsync(TicketId);
            if (result.IsSuccess) Order = result.Value;
            else if (result.Error!.Type != ErrorType.NotFound || Order is not null)
            {
                Accept(result);
                RequiresReload = true;
                return;
            }
        }

        RequiresReload = false;
        Message = Order is null
            ? "Choose a drink, size and optional toppings. Save the draft to review exact snapshots and totals. Showing up to 200 matches; narrow the search for more."
            : $"Ticket reloaded: {Order.Status}. Review the current total before confirming.";
    });

    public void UseCustomerSelection()
    {
        if (!CanEdit) return;
        Customer = customerContext.Request with { AddressLine = customerContext.Selection.AddressLine ?? customerContext.Request.AddressLine };
        CustomerLabel = customerContext.Selection.CustomerName ?? "Walk-in";
        Notify();
    }

    public void WalkIn()
    {
        if (!CanEdit) return;
        Customer = new();
        CustomerLabel = "Walk-in";
        Notify();
    }

    public void AddLine(MenuItemSummary item, SizeVariantSummary size, int quantity, IReadOnlyList<AddonSummary> addons)
    {
        if (!CanEdit) return;
        if (quantity <= 0 || !item.IsActive || !size.IsActive || !item.SizeVariants.Any(variant => variant.Id == size.Id)
            || addons.Any(addon => !addon.IsActive || !item.CompatibleAddons.Any(allowed => allowed.Id == addon.Id))
            || addons.Select(addon => addon.Id).Distinct().Count() != addons.Count)
        {
            ShowValidation("Choose an available size, compatible toppings and a positive whole-number quantity.");
            return;
        }

        Cart = [.. Cart, new(item, size, quantity, addons.ToArray())];
        Error = null;
        Message = "Drink added. Prices are estimates until the draft is saved and reviewed.";
        Notify();
    }

    public void RemoveLine(PosCartLine line)
    {
        if (!CanEdit) return;
        var rows = Cart.ToList();
        rows.Remove(line);
        Cart = rows;
        Notify();
    }

    public Task SaveDraftAsync() => RunAsync(async provider =>
    {
        if (Order is not null || RequiresReload) return;
        var selection = Channel == "delivery" ? Customer with { IsDelivery = true }
            : new CustomerOrderSelectionRequest(Customer.CustomerId);
        var result = await provider.GetRequiredService<PosOrderHandler>().CreateDraftAsync(
            new(TicketId, Channel, Channel == "dine_in" ? TableId : null, selection, Cart.Select(line => line.Request).ToArray()));
        if (Accept(result))
        {
            Order = result.Value;
            Message = "Draft saved. Review the persisted line snapshots, customer details and total, then confirm. Selections are now locked; start a new ticket to change them.";
        }
    });

    public Task ConfirmAsync() => RunAsync(async provider =>
    {
        if (RequiresReload || Order?.Status != OrderStatus.Draft) return;
        var result = await provider.GetRequiredService<PosOrderHandler>().ConfirmAsync(Order.Id, Order.Total);
        if (Accept(result))
        {
            Order = result.Value;
            Message = "Order confirmed. Inventory and table occupancy were committed atomically. Continue payment and fulfillment through the operations workflow.";
        }
    });

    public Task RequestDiscountAsync(string type, decimal value, string reason) => RunAsync(async provider =>
    {
        if (RequiresReload || Order?.Status != OrderStatus.Draft) return;
        var handler = provider.GetRequiredService<PosOrderHandler>();
        var result = await handler.RequestDiscountAsync(new(EntityId.New(), Order.Id, type, value, reason));
        if (!Accept(result)) return;
        RequiresReload = true;
        var detail = await handler.GetOrderAsync(Order.Id);
        if (Accept(detail))
        {
            Order = detail.Value;
            RequiresReload = false;
            Message = "Discount requested, not applied. A manager must approve it. Reload to see the approved total before confirmation.";
        }
    });

    public Task OpenAsync(EntityId id) => RunAsync(async provider =>
    {
        var result = await provider.GetRequiredService<PosOrderHandler>().GetOrderAsync(id);
        if (!Accept(result)) return;
        TicketId = id;
        Order = result.Value;
        RequiresReload = false;
        Message = $"Opened {Order.OrderNumber}: {Order.Status}. Saved ticket details are read-only.";
    });

    public Task RedeemLoyaltyAsync(int points) => RunAsync(async provider =>
    {
        if (RequiresReload || Order?.Status != OrderStatus.Draft) return;
        var result = await provider.GetRequiredService<LoyaltyCommandHandler>()
            .RedeemForOrderAsync(new(Order.Id, points));
        if (!Accept(result)) return;
        RequiresReload = true;
        var detail = await provider.GetRequiredService<PosOrderHandler>().GetOrderAsync(Order.Id);
        if (Accept(detail))
        {
            Order = detail.Value;
            RequiresReload = false;
            Message = $"Redeemed {points} points. Review the reduced payable total before confirmation.";
        }
    });

    public void NewTicket()
    {
        if (IsBusy || RequiresReload) return;
        TicketId = EntityId.New();
        Order = null;
        Cart = [];
        TableId = null;
        Customer = new();
        CustomerLabel = "Walk-in";
        Error = null;
        Message = "New ticket. Any previously saved ticket remains in the database; use its identifier to reopen it.";
        Notify();
    }

    public void ShowValidation(string message)
    {
        Error = Error.Validation("pos.invalid_input", message);
        Message = message;
        Notify();
    }

    private bool Accept(Result result)
    {
        if (result.IsSuccess) return true;
        Error = result.Error;
        Message = $"{Error!.Type}: {Error.Description}" + string.Concat(Error.Details.Select(detail => $"\n{detail.Target}: {detail.Message}"));
        if (Error.Type == ErrorType.Conflict) RequiresReload = true;
        return false;
    }

    private async Task RunAsync(Func<IServiceProvider, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        Error = null;
        Message = "Working...";
        Notify();
        try
        {
            using var scope = scopes.CreateScope();
            await action(scope.ServiceProvider);
        }
        catch (Exception)
        {
            RequiresReload = true;
            Error = Error.Unexpected("pos.database_error", "Could not finish the operation. Check the local database connection and reload this ticket before retrying; the write may have succeeded.");
            Message = Error.Description;
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
