using System.ComponentModel;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using CoffeeShop.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.ViewModels;

public sealed class OperationsWorkspace(IServiceScopeFactory scopes, PosOperatorSession session,
    ApiOperationsSource? api = null) : INotifyPropertyChanged
{
    private EntityId? pendingPaymentId;
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<OperationalOrder> Orders { get; private set; } = [];
    public IReadOnlyList<DeliveryStaffOption> DeliveryStaff { get; private set; } = [];
    public IReadOnlyList<PosStaffOption> Staff { get; private set; } = [];
    public PosStaffOption? Operator
    {
        get => session.Operator;
        set { if (IsBusy) return; session.Operator = value; Notify(); }
    }
    public IReadOnlyDictionary<EntityId, PosOrderDetail> OrderDetails { get; private set; } =
        new Dictionary<EntityId, PosOrderDetail>();
    public EntityId? SelectedOrderId { get; private set; }
    public OperationalOrder? SelectedOrder => Orders.SingleOrDefault(order => order.Id == SelectedOrderId);
    public PosOrderDetail? SelectedDetail => SelectedOrderId is { } id && OrderDetails.TryGetValue(id, out var detail) ? detail : null;
    public bool IsBusy { get; private set; }
    public bool RequiresReload { get; private set; }
    public bool CanMutate => !IsBusy && !RequiresReload;
    public Error? Error { get; private set; }
    public string Message { get; private set; } = "Load operational work to see the current queue and fulfillment controls.";
    public bool IsEmpty => !IsBusy && Error is null && Orders.Count == 0;
    public bool HasOperator => session.IsAuthenticated;
    public bool ApiMode => api is not null;
    public bool CanFulfill => !ApiMode || session.HasPermission("orders.fulfill");
    public bool CanComplete => !ApiMode || session.HasPermission("orders.complete");
    public bool CanRecordPayments => !ApiMode || session.HasPermission("payments.record");
    public bool CanCancelOrders => !ApiMode || session.HasPermission("orders.cancel");
    public bool CanApproveDiscounts => !ApiMode || session.HasPermission("discounts.approve");
    public bool CanAssignDelivery => !ApiMode || session.HasPermission("delivery.assign");
    public bool CanUpdateDelivery => !ApiMode || session.HasPermission("delivery.update");
    public WorkflowFeedback Feedback => WorkflowFeedback.Resolve(IsBusy, IsEmpty, Error, Message);

    public void EnableOperationalWorkflow() => session.EnableOperationalWorkflow();

    public async Task LoadAsync() => await RunAsync(async provider =>
    {
        if (api is not null)
        {
            var snapshot = await api.LoadAsync();
            Orders = snapshot.Orders;
            DeliveryStaff = snapshot.DeliveryStaff;
            Staff = snapshot.Staff;
            session.Operator = Staff.SingleOrDefault();
            await LoadDetailsAsync(provider);
            if (SelectedOrderId is null || Orders.All(order => order.Id != SelectedOrderId))
                SelectedOrderId = Orders.FirstOrDefault(order => order.Status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready)?.Id;
            Message = Orders.Count == 0
                ? "No active orders. Confirm a ticket at Counter to start service."
                : "Choose an order. Controls are enabled only for your permissions and its current lifecycle state.";
            RequiresReload = false;
            pendingPaymentId = null;
            return;
        }
        var queries = provider.GetRequiredService<FulfillmentOperationsQueryHandler>();
        Orders = await queries.GetOrdersAsync();
        DeliveryStaff = await queries.GetDeliveryStaffAsync();
        Staff = (await provider.GetRequiredService<PosOrderHandler>().GetOptionsAsync()).Staff;
        if (session.Operator is { } actor && Staff.All(staff => staff.Id != actor.Id)) session.Operator = null;
        await LoadDetailsAsync(provider);
        if (SelectedOrderId is null || Orders.All(order => order.Id != SelectedOrderId))
            SelectedOrderId = Orders.FirstOrDefault(order => order.Status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready)?.Id;
        Message = Orders.Count == 0
            ? "No active orders. Confirm a ticket at Counter to start service."
            : "Choose an order. Controls are enabled only for its current lifecycle state.";
    });

    public void Select(OperationalOrder? order)
    {
        SelectedOrderId = order?.Id;
        Error = null;
        Notify();
    }

    public void FocusOrder(EntityId id)
    {
        SelectedOrderId = id;
        Notify();
    }

    public void ShowValidation(string message)
    {
        Error = Error.Validation("operations.invalid_input", message);
        Message = message;
        Notify();
    }

    public Task StartPreparingAsync() => TransitionAsync(OrderStatus.Preparing);
    public Task MarkReadyAsync() => TransitionAsync(OrderStatus.Ready);
    public Task CompleteAsync() => TransitionAsync(OrderStatus.Completed);

    public Task RecordPaymentAsync(string method, decimal amount, string reference) => RunAsync(async provider =>
    {
        if (api is not null && RequiresReload) { ShowValidation("Reload this board before retrying a payment."); return; }
        if (SelectedOrder is null) return;
        if (api is not null)
        {
            pendingPaymentId ??= EntityId.New();
            await api.RecordPaymentAsync(pendingPaymentId, SelectedOrder.Id, method, amount,
                string.IsNullOrWhiteSpace(reference) ? null : reference);
            await RefreshAsync(provider, "Payment recorded. Reloaded the latest order balance.");
            pendingPaymentId = null;
            return;
        }
        var result = await provider.GetRequiredService<PaymentCommandHandler>().RecordAsync(
            new(EntityId.New(), SelectedOrder.Id, method, amount, Reference: string.IsNullOrWhiteSpace(reference) ? null : reference));
        if (Accept(result)) await RefreshAsync(provider, $"Payment recorded. Outstanding balance: {result.Value!.OutstandingBalance:0.00}.");
    });

    public Task CancelAsync(string reason) => RunAsync(async provider =>
    {
        if (api is not null && RequiresReload) { ShowValidation("Reload this board before retrying an order change."); return; }
        if (SelectedOrder is null) return;
        if (api is not null)
        {
            await api.CancelAsync(SelectedOrder.Id, reason);
            await RefreshAsync(provider, "Order cancelled. Fulfillment stopped and compensating work was requested.");
            return;
        }
        var result = await provider.GetRequiredService<OrderCancellationCommandHandler>().CancelAsync(new(SelectedOrder.Id, reason));
        if (Accept(result)) await RefreshAsync(provider, "Order cancelled. Fulfillment stopped and compensating work was requested.");
    });

    public Task DecideDiscountAsync(EntityId discountId, bool approve) => RunAsync(async provider =>
    {
        if (api is not null && RequiresReload) { ShowValidation("Reload this board before retrying an order change."); return; }
        if (api is not null)
        {
            await api.DecideDiscountAsync(discountId, approve);
            await RefreshAsync(provider, approve ? "Discount approved and total recalculated." : "Discount rejected.");
            return;
        }
        var handler = provider.GetRequiredService<DiscountCommandHandler>();
        var result = approve ? await handler.ApproveAsync(new(discountId)) : await handler.RejectAsync(new(discountId));
        if (Accept(result)) await RefreshAsync(provider, approve ? "Discount approved and total recalculated." : "Discount rejected.");
    });

    public Task AssignDeliveryAsync(EntityId staffId) => RunAsync(async provider =>
    {
        if (api is not null && RequiresReload) { ShowValidation("Reload this board before retrying an order change."); return; }
        if (SelectedOrder is null || session.Id is null) return;
        if (api is not null)
        {
            await api.AssignDeliveryAsync(SelectedOrder.Id, staffId);
            await RefreshAsync(provider, "Delivery staff assigned.");
            return;
        }
        var result = await provider.GetRequiredService<DeliveryCommandHandler>().AssignAsync(new(SelectedOrder.Id, staffId, session.Id));
        if (Accept(result)) await RefreshAsync(provider, "Delivery staff assigned.");
    });

    public Task StartDeliveryAsync() => api is null
        ? UpdateDeliveryAsync((handler, id, actor) => handler.StartAsync(new(id, actor)), "Delivery is out for delivery.")
        : UpdateDeliveryApiAsync((source, id) => source.StartDeliveryAsync(id), "Delivery is out for delivery.");
    public Task CompleteDeliveryAsync() => api is null
        ? UpdateDeliveryAsync((handler, id, actor) => handler.CompleteAsync(new(id, actor)), "Delivery marked delivered.")
        : UpdateDeliveryApiAsync((source, id) => source.CompleteDeliveryAsync(id), "Delivery marked delivered.");
    public Task FailDeliveryAsync(string reason) => api is null
        ? UpdateDeliveryAsync((handler, id, actor) => handler.FailAsync(new(id, reason, actor)), "Delivery marked failed.")
        : UpdateDeliveryApiAsync((source, id) => source.FailDeliveryAsync(id, reason), "Delivery marked failed.");

    public static bool IsMoveAllowed(OperationalOrder order, string target) => target switch
    {
        OrderStatus.Preparing => order.CanStartPreparing,
        OrderStatus.Ready => order.CanMarkReady,
        OrderStatus.Completed => order.CanComplete,
        _ => false
    };

    public bool CanMoveOrder(EntityId id, string target) => CanMutate && HasOperator
        && (target == OrderStatus.Completed ? CanComplete : CanFulfill)
        && Orders.FirstOrDefault(order => order.Id == id) is { } order && IsMoveAllowed(order, target);

    private Task TransitionAsync(string target) => SelectedOrderId is { } id ? MoveOrderAsync(id, target) : Task.CompletedTask;

    public Task MoveOrderAsync(EntityId id, string target)
    {
        if (IsBusy) return Task.CompletedTask;
        if (!HasOperator)
        {
            ShowValidation("Choose an operator before moving a ticket.");
            return Task.CompletedTask;
        }
        var order = Orders.FirstOrDefault(order => order.Id == id);
        if (order is null || !IsMoveAllowed(order, target))
        {
            ShowValidation(order?.Status == OrderStatus.Ready && target == OrderStatus.Completed && order.OutstandingBalance > 0
                ? "Record the remaining payment before completing this ticket."
                : "This ticket cannot move to that state. Refresh the board and use its next available action.");
            return Task.CompletedTask;
        }
        if (!CanMoveOrder(id, target))
        {
            ShowValidation(target == OrderStatus.Completed
                ? "Your account cannot complete orders. Ask a manager for access."
                : "Your account cannot move preparation tickets.");
            return Task.CompletedTask;
        }
        return RunAsync(async provider =>
        {
            SelectedOrderId = id;
            if (api is not null)
            {
                await api.MoveAsync(id, target);
                await RefreshAsync(provider, $"{order.OrderNumber} moved to {target}.");
                return;
            }
            var result = await provider.GetRequiredService<OrderFulfillmentCommandHandler>().TransitionAsync(new(id, target));
            if (Accept(result)) await RefreshAsync(provider, $"{order.OrderNumber} moved to {target}.");
        });
    }

    private Task UpdateDeliveryAsync(Func<DeliveryCommandHandler, EntityId, EntityId, Task<Result>> action, string success) => RunAsync(async provider =>
    {
        if (SelectedOrder is null || session.Id is null) return;
        var result = await action(provider.GetRequiredService<DeliveryCommandHandler>(), SelectedOrder.Id, session.Id);
        if (Accept(result)) await RefreshAsync(provider, success);
    });

    private Task UpdateDeliveryApiAsync(Func<ApiOperationsSource, EntityId, Task> action, string success) => RunAsync(async provider =>
    {
        if (RequiresReload) { ShowValidation("Reload this board before retrying a delivery change."); return; }
        if (SelectedOrder is null) return;
        await action(api!, SelectedOrder.Id);
        await RefreshAsync(provider, success);
    });

    private async Task RefreshAsync(IServiceProvider provider, string success)
    {
        if (api is not null)
        {
            var snapshot = await api.LoadAsync();
            Orders = snapshot.Orders;
            DeliveryStaff = snapshot.DeliveryStaff;
            Staff = snapshot.Staff;
            await LoadDetailsAsync(provider);
            RequiresReload = false;
            pendingPaymentId = null;
            Message = success;
            return;
        }
        var queries = provider.GetRequiredService<FulfillmentOperationsQueryHandler>();
        Orders = await queries.GetOrdersAsync();
        DeliveryStaff = await queries.GetDeliveryStaffAsync();
        await LoadDetailsAsync(provider);
        Message = success;
    }

    private async Task LoadDetailsAsync(IServiceProvider provider)
    {
        if (api is not null)
        {
            var apiDetails = new Dictionary<EntityId, PosOrderDetail>();
            foreach (var order in Orders.Where(order => order.Status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready
                         || order.Id == SelectedOrderId))
            {
                apiDetails[order.Id] = await api.GetDetailAsync(order.Id);
            }
            OrderDetails = apiDetails;
            return;
        }
        var handler = provider.GetRequiredService<PosOrderHandler>();
        var details = new Dictionary<EntityId, PosOrderDetail>();
        foreach (var order in Orders.Where(order => order.Status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready
                     || order.Id == SelectedOrderId))
        {
            var result = await handler.GetOrderAsync(order.Id);
            if (result.IsSuccess) details[order.Id] = result.Value!;
        }
        OrderDetails = details;
    }

    private bool Accept(Result result)
    {
        if (result.IsSuccess) return true;
        Error = result.Error;
        Message = $"{Error!.Type}: {Error.Description}";
        return false;
    }

    private async Task RunAsync(Func<IServiceProvider, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Error = null; Notify();
        try { using var scope = scopes.CreateScope(); await action(scope.ServiceProvider); }
        catch (ApiException exception)
        {
            if (exception.Kind is ApiErrorKind.Conflict or ApiErrorKind.Network or ApiErrorKind.Server or ApiErrorKind.Unexpected)
                RequiresReload = true;
            else if (exception.Kind is ApiErrorKind.Validation or ApiErrorKind.Unauthorized or ApiErrorKind.Forbidden or ApiErrorKind.NotFound)
                pendingPaymentId = null;
            Error = exception.Kind switch
            {
                ApiErrorKind.Validation => Error.Validation("operations.api_validation", exception.Detail),
                ApiErrorKind.Unauthorized => Error.Authentication("operations.api_authentication", exception.Detail),
                ApiErrorKind.Forbidden => Error.Authorization("operations.api_forbidden", exception.Detail),
                ApiErrorKind.NotFound => Error.NotFound("operations.api_not_found", exception.Detail),
                ApiErrorKind.Conflict => Error.Conflict("operations.api_conflict", exception.Detail),
                ApiErrorKind.BusinessRule => Error.BusinessRule("operations.api_rule", exception.Detail),
                _ => Error.Unexpected("operations.api_unavailable", "Could not finish the operation. Reload before retrying because a write may have succeeded."),
            };
            Message = Error.Description;
        }
        catch (Exception)
        {
            if (api is not null) RequiresReload = true;
            Error = Error.Unexpected(api is null ? "operations.database_error" : "operations.api_unavailable",
                "Could not finish the operation. Reload before retrying because a write may have succeeded.");
            Message = Error.Description;
        }
        finally { IsBusy = false; Notify(); }
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
