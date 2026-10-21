using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class OperationsWorkspaceTests
{
    [Fact]
    public void BaristaControls_FollowTheOrderStateMachine()
    {
        var confirmed = Order(OrderStatus.Confirmed);
        var preparing = Order(OrderStatus.Preparing);
        var readyUnpaid = Order(OrderStatus.Ready, total: 30m, paid: 10m);
        var readyPaid = Order(OrderStatus.Ready, total: 30m, paid: 30m);

        Assert.True(confirmed.CanStartPreparing);
        Assert.False(confirmed.CanMarkReady);
        Assert.True(preparing.CanMarkReady);
        Assert.False(readyUnpaid.CanComplete);
        Assert.True(readyPaid.CanComplete);
    }

    [Fact]
    public void PaymentAndCancellationControls_AreHiddenForTerminalOrders()
    {
        var completed = Order(OrderStatus.Completed);
        var cancelled = Order(OrderStatus.Cancelled);

        Assert.False(completed.CanRecordPayment);
        Assert.False(completed.CanCancel);
        Assert.False(cancelled.CanRecordPayment);
        Assert.False(cancelled.CanCancel);
    }

    [Fact]
    public void DeliveryControls_FollowDeliveryStatusAndReadyOrder()
    {
        var ready = Order(OrderStatus.Ready, channel: "delivery", deliveryStatus: DeliveryStatus.Unassigned);
        var assigned = Order(OrderStatus.Ready, channel: "delivery", deliveryStatus: DeliveryStatus.Assigned);
        var outForDelivery = Order(OrderStatus.Ready, channel: "delivery", deliveryStatus: DeliveryStatus.OutForDelivery);

        Assert.True(ready.CanAssignDelivery);
        Assert.True(assigned.CanStartDelivery);
        Assert.True(outForDelivery.CanFinishDelivery);
        Assert.False(Order(OrderStatus.Preparing, channel: "delivery", deliveryStatus: DeliveryStatus.Unassigned).CanAssignDelivery);
    }

    [Fact]
    public async Task DatabaseFailure_IsSafeAndLeavesFulfillmentReloadable()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new OperationsWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new());

        await model.LoadAsync();

        Assert.Equal(WorkflowFeedbackKind.DatabaseError, model.Feedback.Kind);
        Assert.Contains("reload", model.Feedback.DisplayMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(model.IsBusy);
    }

    private static OperationalOrder Order(string status, decimal total = 20m, decimal paid = 0m,
        string channel = "takeaway", string? deliveryStatus = null) =>
        new(EntityId.New(), "ORD-1", channel, status, total, paid, null, deliveryStatus, null, []);
}
