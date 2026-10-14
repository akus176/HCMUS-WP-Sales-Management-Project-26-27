using System.ComponentModel;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.ViewModels;

// Presentation orchestration only; validation and persistence live in Application.
public sealed class CustomerWorkspace(IServiceScopeFactory scopes, CustomerOrderContext orderContext) : INotifyPropertyChanged
{
    public const int HistoryPageSize = 20;
    private EntityId? selectedId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Search { get; set; } = "";
    public bool IncludeInactive { get; set; }
    public bool IsBusy { get; private set; }
    public bool RequiresReload { get; private set; }
    public Error? Error { get; private set; }
    public bool IsError => Error is not null;
    public string Message { get; private set; } = "Load customers to begin.";
    public IReadOnlyList<CustomerSummary> Customers { get; private set; } = [];
    public CustomerDetail? SelectedCustomer { get; private set; }
    public LoyaltyHistoryPage History { get; private set; } = new([], 0, HistoryPageSize, 0);
    public CustomerOrderSelection OrderSelection => orderContext.Selection;
    public CustomerOrderSelectionRequest OrderRequest => orderContext.Request;
    public bool IsEmpty => !IsBusy && !IsError && Customers.Count == 0;
    public bool CanEdit => !IsBusy && !RequiresReload && SelectedCustomer is { IsActive: true };
    public bool CanPrevious => !IsBusy && !RequiresReload && History.Offset > 0;
    public bool CanNext => !IsBusy && !RequiresReload && History.Offset + History.Items.Count < History.TotalCount;

    public Task LoadAsync() => RunAsync(async () =>
    {
        await ReloadCoreAsync();
        if (!IsError)
        {
            Message = Customers.Count == 100
                ? "Showing the first 100 matches. Narrow the search to find more customers."
                : "Customers loaded. Select a profile or create a new one.";
        }
    });

    public Task SelectAsync(EntityId id) => RunAsync(async () =>
    {
        selectedId = id;
        SelectedCustomer = null;
        History = new([], 0, HistoryPageSize, 0);
        using var scope = scopes.CreateScope();
        await LoadDetailAsync(scope.ServiceProvider.GetRequiredService<CustomerQueryHandler>(), 0);
        if (!IsError)
        {
            Message = "Profile loaded. Loyalty history is read-only.";
        }
    });

    public Task PreviousHistoryAsync() => LoadHistoryAsync(Math.Max(0, History.Offset - HistoryPageSize));

    public Task NextHistoryAsync() => LoadHistoryAsync(History.Offset + HistoryPageSize);

    // Called after the sales workflow has committed completion and its loyalty
    // command. Refreshing a view never awards points or writes ledger entries.
    public Task RefreshAfterOrderCompletionAsync(EntityId customerId)
    {
        if (selectedId == customerId && !IsBusy)
        {
            History = new([], 0, HistoryPageSize, 0);
        }

        return LoadAsync();
    }

    public Task<bool> CreateAsync(CreateCustomerCommand command) => SaveAsync(async commands =>
    {
        var result = await commands.CreateCustomerAsync(command);
        if (result.IsSuccess)
        {
            selectedId = result.Value;
        }

        return result;
    }, "Customer created.");

    public Task<bool> UpdateAsync(UpdateCustomerCommand command) =>
        SaveAsync(commands => commands.UpdateCustomerAsync(command), "Customer saved.");

    public Task<bool> DeactivateAsync(EntityId customerId) => SaveAsync(async commands =>
    {
        var result = await commands.DeactivateCustomerAsync(new(customerId));
        if (result.IsSuccess && OrderSelection.CustomerId == customerId)
        {
            orderContext.Clear();
        }

        return result;
    }, "Customer deactivated. Historical orders and loyalty entries are preserved.");

    public Task<bool> AddAddressAsync(AddCustomerAddressCommand command) =>
        SaveAsync(async commands => await commands.AddAddressAsync(command), "Address saved.");

    public Task<bool> UpdateAddressAsync(UpdateCustomerAddressCommand command) =>
        SaveAsync(commands => commands.UpdateAddressAsync(command), "Address saved.");

    public Task<bool> SetDefaultAddressAsync(EntityId customerId, EntityId addressId) =>
        SaveAsync(commands => commands.SetDefaultAddressAsync(new(customerId, addressId)), "Default address saved.");

    public Task<bool> DeactivateAddressAsync(EntityId customerId, EntityId addressId) => SaveAsync(async commands =>
    {
        var result = await commands.DeactivateAddressAsync(new(customerId, addressId));
        if (result.IsSuccess && OrderSelection.Delivery?.CustomerAddressId == addressId)
        {
            orderContext.Clear();
        }

        return result;
    }, "Address deactivated. Existing delivery snapshots are preserved.");

    public async Task<bool> SetOrderSelectionAsync(CustomerOrderSelectionRequest request)
    {
        var saved = false;
        await RunAsync(async () =>
        {
            if (RequiresReload)
            {
                ShowValidation("Reload customers before changing the order selection.");
                return;
            }

            using var scope = scopes.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<CustomerOrderSelectionHandler>().PrepareAsync(request);
            if (Accept(result))
            {
                orderContext.Set(request, result.Value);
                saved = true;
                Message = "Customer and delivery details selected for the next order.";
            }
        });
        return saved;
    }

    public void ContinueAsWalkIn()
    {
        if (IsBusy)
        {
            return;
        }

        orderContext.Clear();
        Error = null;
        Message = "Walk-in selected. No customer profile is required.";
        Notify();
    }

    private async Task<bool> SaveAsync(Func<CustomerCommandHandler, Task<Result>> action, string successMessage)
    {
        var saved = false;
        await RunAsync(async () =>
        {
            if (RequiresReload)
            {
                ShowValidation("Reload customers and verify the saved state before making changes.");
                return;
            }

            using (var scope = scopes.CreateScope())
            {
                saved = Accept(await action(scope.ServiceProvider.GetRequiredService<CustomerCommandHandler>()));
            }

            if (!saved)
            {
                return;
            }

            await ReloadCoreAsync();
            Message = IsError
                ? $"{successMessage} Refresh failed. Reload before continuing; do not submit again."
                : successMessage;
        }, successMessage: () => saved ? successMessage : null);
        return saved;
    }

    private async Task ReloadCoreAsync()
    {
        using var scope = scopes.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<CustomerQueryHandler>();
        var result = await queries.SearchCustomersAsync(new(Search, IncludeInactive, 100));
        if (!Accept(result))
        {
            RequiresReload = true;
            return;
        }

        Customers = result.Value;
        RequiresReload = false;
        if (selectedId is not null)
        {
            await LoadDetailAsync(queries, History.Offset);
        }

        if (!IsError && OrderSelection.CustomerId is not null)
        {
            var prepared = await scope.ServiceProvider.GetRequiredService<CustomerOrderSelectionHandler>()
                .PrepareAsync(orderContext.Request);
            if (prepared.IsSuccess)
            {
                orderContext.Set(orderContext.Request, prepared.Value);
            }
            else
            {
                orderContext.Clear();
            }
        }
    }

    private async Task LoadDetailAsync(CustomerQueryHandler queries, int offset)
    {
        var customerId = selectedId!;
        var detail = await queries.GetCustomerDetailAsync(customerId);
        if (!Accept(detail))
        {
            if (detail.Error!.Type == ErrorType.NotFound)
            {
                selectedId = null;
                if (OrderSelection.CustomerId == customerId)
                {
                    orderContext.Clear();
                }
            }

            SelectedCustomer = null;
            History = new([], 0, HistoryPageSize, 0);
            RequiresReload = true;
            return;
        }

        var history = await queries.GetLoyaltyHistoryAsync(selectedId!, new(offset, HistoryPageSize));
        if (!Accept(history))
        {
            RequiresReload = true;
            return;
        }

        SelectedCustomer = detail.Value;
        History = history.Value;
        RequiresReload = false;
        if (!SelectedCustomer.IsActive && OrderSelection.CustomerId == SelectedCustomer.Id)
        {
            orderContext.Clear();
        }
    }

    private Task LoadHistoryAsync(int offset) => RunAsync(async () =>
    {
        if (selectedId is null || RequiresReload || offset < 0 || offset >= History.TotalCount)
        {
            return;
        }

        using var scope = scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<CustomerQueryHandler>()
            .GetLoyaltyHistoryAsync(selectedId, new(offset, HistoryPageSize));
        if (Accept(result))
        {
            History = result.Value;
            Message = "Loyalty history loaded.";
        }
        else
        {
            RequiresReload = true;
        }
    });

    private bool Accept(Result result)
    {
        if (result.IsSuccess)
        {
            return true;
        }

        Error = result.Error;
        Message = result.Error!.Description;
        return false;
    }

    public void ShowValidation(string message)
    {
        Error = Error.Validation("customers.ui_validation", message);
        Message = message;
        Notify();
    }

    private async Task RunAsync(Func<Task> action, Func<string?>? successMessage = null)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        Message = "Working...";
        Notify();
        try
        {
            await action();
        }
        catch (Exception)
        {
            RequiresReload = true;
            var saved = successMessage?.Invoke();
            Message = saved is null
                ? "The operation could not complete. Check the database connection, then reload and verify the saved state before retrying."
                : $"{saved} Refresh failed. Reload before continuing; do not submit again.";
            Error = Error.Unexpected("customers.database_error", Message);
        }
        finally
        {
            IsBusy = false;
            Notify();
        }
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
