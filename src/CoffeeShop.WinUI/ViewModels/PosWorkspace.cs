using System.ComponentModel;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using CoffeeShop.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.ViewModels;

public sealed record PosCartLine(MenuItemSummary Item, SizeVariantSummary Size, int Quantity, IReadOnlyList<AddonSummary> Addons)
{
    public Guid LineId { get; init; } = Guid.NewGuid();
    public string UnitPrice => $"{Size.Price + Addons.Sum(addon => addon.Price):0.00} each";
    public string DecreaseLabel => $"Decrease quantity of {Item.Name}";
    public string IncreaseLabel => $"Increase quantity of {Item.Name}";
    public string RemoveLabel => $"Remove {Item.Name} from ticket";
    public string Description => $"{Quantity} x {Item.Name} / {Size.SizeName} @ {Size.Price:0.00}"
        + string.Concat(Addons.Select(addon => $"\n+ {addon.Name} @ {addon.Price:0.00} per drink"));
    public string DisplayName => Item.Name;
    public string Details => Addons.Count == 0 ? Size.SizeName : $"{Size.SizeName} · {string.Join(" · ", Addons.Select(addon => addon.Name))}";
    public string Amount => ((Size.Price + Addons.Sum(addon => addon.Price)) * Quantity).ToString("0.00");
    public PosLineRequest Request => new(Item.Id, Size.Id, Quantity, Addons.Select(addon => addon.Id).ToArray());
}

public sealed class PosWorkspace(IServiceScopeFactory scopes, CustomerOrderContext customerContext, PosOperatorSession session,
    ApiPosSource? api = null) : INotifyPropertyChanged
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
    public IReadOnlyList<CategorySummary> Categories { get; private set; } = [];
    public EntityId? CategoryId { get; private set; }
    public IReadOnlyList<MenuItemSummary> VisibleMenu => CategoryId is { } id
        ? Menu.Where(item => item.CategoryId == id).ToArray() : Menu;
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
        var queries = api is null ? provider.GetRequiredService<CatalogQueryHandler>() : null;
        var menu = api is null ? await queries!.SearchMenuItemsAsync(new(Search: Search, IsActive: true, Limit: 200))
            : Result<IReadOnlyList<MenuItemSummary>>.Success(await api.MenuAsync(Search));
        var categories = api is null ? await queries!.SearchCategoriesAsync(new(IsActive: true, Limit: 200))
            : Result<IReadOnlyList<CategorySummary>>.Success(await api.CategoriesAsync());
        if (!Accept(menu) || !Accept(categories)) return;
        var activeCategories = categories.Value.Select(category => category.Id).ToHashSet();
        Categories = categories.Value;
        if (CategoryId is { } selectedCategory && !activeCategories.Contains(selectedCategory)) CategoryId = null;
        Menu = menu.Value.Where(item => activeCategories.Contains(item.CategoryId) && item.SizeVariants.Any(size => size.IsActive)).ToArray();
        var handler = api is null ? provider.GetRequiredService<PosOrderHandler>() : null;
        Options = api is null ? await handler!.GetOptionsAsync() : await api.OptionsAsync();
        if (api is not null) session.Operator = Options.Staff.SingleOrDefault();
        if (session.Operator is { } actor && Options.Staff.All(staff => staff.Id != actor.Id)) session.Operator = null;
        if (Order is not null || RequiresReload)
        {
            var result = api is null ? await handler!.GetOrderAsync(TicketId)
                : Result<PosOrderDetail>.Success(await api.GetAsync(TicketId));
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
        if (rows.RemoveAll(row => row.LineId == line.LineId) == 0) return;
        Cart = rows;
        Error = null;
        Message = "Item removed from the ticket.";
        Notify();
    }

    public void ChangeQuantity(PosCartLine line, int quantity)
    {
        if (!CanEdit) return;
        if (quantity <= 0)
        {
            RemoveLine(line);
            return;
        }
        var rows = Cart.ToList();
        var index = rows.FindIndex(row => row.LineId == line.LineId);
        if (index < 0) return;
        rows[index] = rows[index] with { Quantity = quantity };
        Cart = rows;
        Error = null;
        Message = "Ticket quantity updated. Save the draft to review exact totals.";
        Notify();
    }

    public void SelectCategory(EntityId? id)
    {
        CategoryId = id;
        Notify();
    }

    public Task SaveDraftAsync() => RunAsync(async provider =>
    {
        if (Order is not null || RequiresReload) return;
        var selection = Channel == "delivery" ? Customer with { IsDelivery = true }
            : new CustomerOrderSelectionRequest(Customer.CustomerId);
        var lines = Cart.Select(line => line.Request).ToArray();
        var result = api is null ? await provider.GetRequiredService<PosOrderHandler>().CreateDraftAsync(
            new(TicketId, Channel, Channel == "dine_in" ? TableId : null, selection, lines))
            : Result<PosOrderDetail>.Success(await api.CreateAsync(TicketId, Channel, Channel == "dine_in" ? TableId : null, selection, lines));
        if (Accept(result))
        {
            Order = result.Value;
            Message = "Draft saved. Review the persisted line snapshots, customer details and total, then confirm. Selections are now locked; start a new ticket to change them.";
        }
    });

    public Task ConfirmAsync() => RunAsync(async provider =>
    {
        if (RequiresReload || Order?.Status != OrderStatus.Draft) return;
        var result = api is null ? await provider.GetRequiredService<PosOrderHandler>().ConfirmAsync(Order.Id, Order.Total)
            : Result<PosOrderDetail>.Success(await api.ConfirmAsync(Order.Id, Order.Total));
        if (Accept(result))
        {
            Order = result.Value;
            Message = "Order confirmed. Inventory and table occupancy were committed atomically. Continue payment and fulfillment through the operations workflow.";
        }
    });

    public Task RequestDiscountAsync(string type, decimal value, string reason) => RunAsync(async provider =>
    {
        if (RequiresReload || Order?.Status != OrderStatus.Draft) return;
        var handler = api is null ? provider.GetRequiredService<PosOrderHandler>() : null;
        var discountId = EntityId.New();
        if (api is null)
        {
            var result = await handler!.RequestDiscountAsync(new(discountId, Order.Id, type, value, reason));
            if (!Accept(result)) return;
        }
        else await api.RequestDiscountAsync(Order.Id, discountId, type, value, reason);
        RequiresReload = true;
        var detail = api is null ? await handler!.GetOrderAsync(Order.Id)
            : Result<PosOrderDetail>.Success(await api.GetAsync(Order.Id));
        if (Accept(detail))
        {
            Order = detail.Value;
            RequiresReload = false;
            Message = "Discount requested, not applied. A manager must approve it. Reload to see the approved total before confirmation.";
        }
    });

    public Task OpenAsync(EntityId id) => RunAsync(async provider =>
    {
        var result = api is null ? await provider.GetRequiredService<PosOrderHandler>().GetOrderAsync(id)
            : Result<PosOrderDetail>.Success(await api.GetAsync(id));
        if (!Accept(result)) return;
        TicketId = id;
        Order = result.Value;
        RequiresReload = false;
        Message = $"Opened {Order.OrderNumber}: {Order.Status}. Saved ticket details are read-only.";
    });

    public Task RedeemLoyaltyAsync(int points) => RunAsync(async provider =>
    {
        if (RequiresReload || Order?.Status != OrderStatus.Draft) return;
        if (api is null)
        {
            var result = await provider.GetRequiredService<LoyaltyCommandHandler>().RedeemForOrderAsync(new(Order.Id, points));
            if (!Accept(result)) return;
        }
        else await api.RedeemAsync(Order.Id, points);
        RequiresReload = true;
        var detail = api is null ? await provider.GetRequiredService<PosOrderHandler>().GetOrderAsync(Order.Id)
            : Result<PosOrderDetail>.Success(await api.GetAsync(Order.Id));
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
        CategoryId = null;
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
        catch (ApiException exception)
        {
            RequiresReload = exception.Kind is ApiErrorKind.Conflict or ApiErrorKind.Network or ApiErrorKind.Server or ApiErrorKind.Unexpected;
            Error = exception.Kind switch
            {
                ApiErrorKind.Validation => Error.Validation("pos.api_validation", exception.Detail),
                ApiErrorKind.Unauthorized => Error.Authentication("pos.api_authentication", exception.Detail),
                ApiErrorKind.Forbidden => Error.Authorization("pos.api_forbidden", exception.Detail),
                ApiErrorKind.NotFound => Error.NotFound("pos.api_not_found", exception.Detail),
                ApiErrorKind.Conflict => Error.Conflict("pos.api_conflict", exception.Detail),
                ApiErrorKind.BusinessRule => Error.BusinessRule("pos.api_rule", exception.Detail),
                _ => Error.Unexpected("pos.api_unavailable", "Could not finish the operation. Check the connection and reload the ticket before retrying; the write may have succeeded."),
            };
            Message = Error.Description;
        }
        catch (Exception)
        {
            RequiresReload = true;
            Error = Error.Unexpected(api is null ? "pos.database_error" : "pos.api_unavailable", api is null
                ? "Could not finish the operation. Check the local database connection and reload this ticket before retrying; the write may have succeeded."
                : "Could not finish the operation. Check the API connection and reload this ticket before retrying; the write may have succeeded.");
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
