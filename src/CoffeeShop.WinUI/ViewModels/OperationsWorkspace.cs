using System.ComponentModel;
using CoffeeShop.Application.Common.Results;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.ViewModels;

public sealed class OperationsWorkspace(IServiceScopeFactory scopes, PosOperatorSession session) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<OperationalOrder> Orders { get; private set; } = [];
    public IReadOnlyList<OperationalTable> Tables { get; private set; } = [];
    public IReadOnlyList<DeliveryStaffOption> DeliveryStaff { get; private set; } = [];
    public IReadOnlyDictionary<EntityId, PosOrderDetail> OrderDetails { get; private set; } =
        new Dictionary<EntityId, PosOrderDetail>();
    public EntityId? SelectedOrderId { get; private set; }
    public OperationalOrder? SelectedOrder => Orders.SingleOrDefault(order => order.Id == SelectedOrderId);
    public PosOrderDetail? SelectedDetail => SelectedOrderId is { } id && OrderDetails.TryGetValue(id, out var detail) ? detail : null;
    public bool IsBusy { get; private set; }
    public Error? Error { get; private set; }
    public string Message { get; private set; } = "Load operational work to see the current queue, tables and fulfillment controls.";
    public bool IsEmpty => !IsBusy && Error is null && Orders.Count == 0 && Tables.Count == 0;
    public bool HasOperator => session.IsAuthenticated;
    public WorkflowFeedback Feedback => WorkflowFeedback.Resolve(IsBusy, IsEmpty, Error, Message);

    public void EnableOperationalWorkflow() => session.EnableOperationalWorkflow();

    public async Task LoadAsync() => await RunAsync(async provider =>
    {
        var queries = provider.GetRequiredService<FulfillmentOperationsQueryHandler>();
        Orders = await queries.GetOrdersAsync();
        Tables = await queries.GetTablesAsync();
        DeliveryStaff = await queries.GetDeliveryStaffAsync();
        await LoadDetailsAsync(provider);
        if (SelectedOrderId is null || Orders.All(order => order.Id != SelectedOrderId))
            SelectedOrderId = Orders.FirstOrDefault(order => order.Status is OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready)?.Id;
        Message = Orders.Count == 0 && Tables.Count == 0
            ? "No operational records yet. Create and confirm an order from Point of Sale to begin."
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
        if (SelectedOrder is null) return;
        var result = await provider.GetRequiredService<PaymentCommandHandler>().RecordAsync(
            new(EntityId.New(), SelectedOrder.Id, method, amount, Reference: string.IsNullOrWhiteSpace(reference) ? null : reference));
        if (Accept(result)) await RefreshAsync(provider, $"Payment recorded. Outstanding balance: {result.Value!.OutstandingBalance:0.00}.");
    });

    public Task CancelAsync(string reason) => RunAsync(async provider =>
    {
        if (SelectedOrder is null) return;
        var result = await provider.GetRequiredService<OrderCancellationCommandHandler>().CancelAsync(new(SelectedOrder.Id, reason));
        if (Accept(result)) await RefreshAsync(provider, "Order cancelled. Fulfillment stopped and compensating work was requested.");
    });

    public Task DecideDiscountAsync(EntityId discountId, bool approve) => RunAsync(async provider =>
    {
        var handler = provider.GetRequiredService<DiscountCommandHandler>();
        var result = approve ? await handler.ApproveAsync(new(discountId)) : await handler.RejectAsync(new(discountId));
        if (Accept(result)) await RefreshAsync(provider, approve ? "Discount approved and total recalculated." : "Discount rejected.");
    });

    public Task AssignDeliveryAsync(EntityId staffId) => RunAsync(async provider =>
    {
        if (SelectedOrder is null || session.Id is null) return;
        var result = await provider.GetRequiredService<DeliveryCommandHandler>().AssignAsync(new(SelectedOrder.Id, staffId, session.Id));
        if (Accept(result)) await RefreshAsync(provider, "Delivery staff assigned.");
    });

    public Task StartDeliveryAsync() => UpdateDeliveryAsync((handler, id, actor) => handler.StartAsync(new(id, actor)), "Delivery is out for delivery.");
    public Task CompleteDeliveryAsync() => UpdateDeliveryAsync((handler, id, actor) => handler.CompleteAsync(new(id, actor)), "Delivery marked delivered.");
    public Task FailDeliveryAsync(string reason) => UpdateDeliveryAsync((handler, id, actor) => handler.FailAsync(new(id, reason, actor)), "Delivery marked failed.");

    private Task TransitionAsync(string target) => RunAsync(async provider =>
    {
        if (SelectedOrder is null) return;
        var result = await provider.GetRequiredService<OrderFulfillmentCommandHandler>().TransitionAsync(new(SelectedOrder.Id, target));
        if (Accept(result)) await RefreshAsync(provider, $"Order moved to {target}.");
    });

    private Task UpdateDeliveryAsync(Func<DeliveryCommandHandler, EntityId, EntityId, Task<Result>> action, string success) => RunAsync(async provider =>
    {
        if (SelectedOrder is null || session.Id is null) return;
        var result = await action(provider.GetRequiredService<DeliveryCommandHandler>(), SelectedOrder.Id, session.Id);
        if (Accept(result)) await RefreshAsync(provider, success);
    });

    private async Task RefreshAsync(IServiceProvider provider, string success)
    {
        var queries = provider.GetRequiredService<FulfillmentOperationsQueryHandler>();
        Orders = await queries.GetOrdersAsync();
        Tables = await queries.GetTablesAsync();
        DeliveryStaff = await queries.GetDeliveryStaffAsync();
        await LoadDetailsAsync(provider);
        Message = success;
    }

    private async Task LoadDetailsAsync(IServiceProvider provider)
    {
        var handler = provider.GetRequiredService<PosOrderHandler>();
        var details = new Dictionary<EntityId, PosOrderDetail>();
        foreach (var order in Orders)
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
        catch (Exception)
        {
            Error = Error.Unexpected("operations.database_error", "Could not finish the operation. Reload before retrying because a write may have succeeded.");
            Message = Error.Description;
        }
        finally { IsBusy = false; Notify(); }
    }

    private void Notify() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
