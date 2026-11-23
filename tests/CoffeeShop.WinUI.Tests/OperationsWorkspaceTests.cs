using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Sales;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinUI.Tests;

public sealed class OperationsWorkspaceTests
{
    [Fact]
    public void BoardMoves_AllowOnlyForwardStates_AndRequirePaymentBeforeCompletion()
    {
        Assert.True(OperationsWorkspace.IsMoveAllowed(Order(OrderStatus.Confirmed), OrderStatus.Preparing));
        Assert.True(OperationsWorkspace.IsMoveAllowed(Order(OrderStatus.Preparing), OrderStatus.Ready));
        Assert.False(OperationsWorkspace.IsMoveAllowed(Order(OrderStatus.Confirmed), OrderStatus.Ready));
        Assert.False(OperationsWorkspace.IsMoveAllowed(Order(OrderStatus.Preparing), OrderStatus.Confirmed));
        Assert.False(OperationsWorkspace.IsMoveAllowed(Order(OrderStatus.Ready, 30, 10), OrderStatus.Completed));
        Assert.True(OperationsWorkspace.IsMoveAllowed(Order(OrderStatus.Ready, 30, 30), OrderStatus.Completed));
        Assert.False(OperationsWorkspace.IsMoveAllowed(Order(OrderStatus.Completed), OrderStatus.Preparing));
        Assert.False(OperationsWorkspace.IsMoveAllowed(Order(OrderStatus.Confirmed), OrderStatus.Cancelled));
    }

    [Fact]
    public async Task BoardMoveWithoutOperator_ShowsValidation_WithoutAccessingPersistence()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var model = new OperationsWorkspace(services.GetRequiredService<IServiceScopeFactory>(), new());
        await model.MoveOrderAsync(EntityId.New(), OrderStatus.Preparing);
        Assert.Equal("operations.invalid_input", model.Error?.Code);
        Assert.Contains("operator", model.Error!.Description);
        Assert.Equal(WorkflowFeedbackKind.Validation, model.Feedback.Kind);
        Assert.False(model.IsBusy);
    }
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
    public void ServiceBoardTables_ShowServerAvailabilityAndAssociatedOrder()
    {
        var available = new OperationalTable(EntityId.New(), "T1", 2, false, null);
        var orderId = EntityId.New();
        var occupied = new OperationalTable(EntityId.New(), "T2", 4, true, orderId);
        var orders = new[]
        {
            new OperationalOrder(orderId, "ORD-42", "dine_in", OrderStatus.Confirmed, 20m, 0m, "T2", null, null, [])
        };

        var rows = OperationsWorkspace.BuildTableRows([occupied, available], orders);

        Assert.Collection(rows,
            row => Assert.Equal(("T1", "Available", "No active order", "Table T1, available"),
                (row.TableNumber, row.Availability, row.Association, row.AccessibilityLabel)),
            row => Assert.Equal(("T2", "Occupied", "ORD-42", "Table T2, occupied, order ORD-42"),
                (row.TableNumber, row.Availability, row.Association, row.AccessibilityLabel)));
    }

    [Fact]
    public void ServiceBoardTable_ReportsOccupiedWhenOrderSummaryIsNotLoaded()
    {
        var table = new OperationalTable(EntityId.New(), "T3", null, true, EntityId.New());

        var row = OperationsWorkspace.BuildTableRows([table], []).Single();

        Assert.Equal("Occupied", row.Availability);
        Assert.Equal("Order details unavailable", row.Association);
        Assert.Equal("Table T3, occupied, order details unavailable", row.AccessibilityLabel);
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
