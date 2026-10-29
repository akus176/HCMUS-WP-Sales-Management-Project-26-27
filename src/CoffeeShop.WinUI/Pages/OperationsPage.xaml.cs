using System.ComponentModel;
using System.Globalization;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Automation;

namespace CoffeeShop.WinUI.Pages;

public sealed record ServiceBoardCard(OperationalOrder Order, PosOrderDetail? Detail, bool IsSelected, bool CanInteract = false)
{
    public string Status => char.ToUpperInvariant(Order.Status[0]) + Order.Status[1..];
    public string Number => Order.OrderNumber;
    public string ChannelLabel => Order.TableNumber is { } table ? $"Table {table}" : Order.Channel == "delivery" ? "Delivery" : "Takeaway";
    public string Headline => Detail?.CustomerName ?? (Order.TableNumber is { } table ? $"Table {table}" : "Walk-in");
    public string Items => Detail is { Lines.Count: > 0 } detail
        ? string.Join(" · ", detail.Lines.Select(line => $"{(line.Quantity > 1 ? $"{line.Quantity}× " : "")}{line.ItemName}"))
        : "Order details available on selection";
    public string Footer => IsSelected ? "Selected" : Order.Channel == "dine_in" ? "Dine-in" : Order.Channel == "delivery" ? "Delivery" : "Takeaway";
    public string BalanceLabel => Order.OutstandingBalance > 0 ? $"Due {Order.OutstandingBalance:0.00}" : "Paid";
    public string NextStatus => Order.Status switch { "confirmed" => "preparing", "preparing" => "ready", "ready" => "completed", _ => "" };
    public string ActionLabel => Order.Status switch { "confirmed" => "Start preparing", "preparing" => "Mark ready", "ready" when Order.OutstandingBalance > 0 => "Review payment", "ready" => "Complete", _ => "" };
    public bool ActionEnabled => CanInteract && (OperationsWorkspace.IsMoveAllowed(Order, NextStatus) || Order.Status == "ready" && Order.OutstandingBalance > 0);
    public string ActionName => $"{ActionLabel}: {Number}";
    public string ActionHint => !CanInteract ? "Choose an operator and wait for the board to finish loading."
        : Order.Status == "ready" && Order.OutstandingBalance > 0 ? "Select this ticket and record its outstanding payment before completing it."
        : $"Move {Number} to {NextStatus}.";
}

public sealed record ServiceBoardDiscount(EntityId Id, string Label);
public sealed record ServiceBoardLine(PosLineSnapshot Line)
{
    public int Quantity => Line.Quantity;
    public string ItemName => Line.ItemName;
    public string Description => string.Join(" · ", new[] { Line.SizeName }.Concat(Line.Addons.Select(addon => addon.Name)));
    public string TotalText => Line.Total.ToString("0.00", CultureInfo.CurrentCulture);
}

public sealed partial class OperationsPage : Page
{
    private readonly OperationsWorkspace model;
    private bool rendering;
    private EntityId? draggedOrderId;
    private PosStaffOption? verificationOperator;
    private EntityId? formOrderId;
    private decimal? formBalance;

    public OperationsPage()
    {
        InitializeComponent();
        model = App.Services.GetRequiredService<OperationsWorkspace>();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        model.PropertyChanged += OnChanged;
        model.EnableOperationalWorkflow();
        await model.LoadAsync();
        ApplyBoardLayout();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args) => model.PropertyChanged -= OnChanged;
    private void OnChanged(object? sender, PropertyChangedEventArgs args) => Render();
    private async void OnReload(object sender, RoutedEventArgs args) => await model.LoadAsync();
    private void OnOpenCounter(object sender, RoutedEventArgs args)
    {
        if (((App)Microsoft.UI.Xaml.Application.Current).Window is MainWindow main) main.NavigateTo("counter");
    }
    private void OnOrderChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering || sender is not ListView { SelectedItem: ServiceBoardCard card }) return;
        model.Select(card.Order);
        if (Grid.GetRow(InspectorScroll) == 1)
            DispatcherQueue.TryEnqueue(() => SelectedTitle.StartBringIntoView());
    }
    private void OnOperatorChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering) model.Operator = OperatorBox.SelectedItem as PosStaffOption;
    }

    private void OnBoardSizeChanged(object sender, SizeChangedEventArgs args) => ApplyBoardLayout();

    private void ApplyBoardLayout()
    {
        if (BoardLayout is null || ActualWidth <= 0) return;
        var sideInspector = ActualWidth >= 1160 && ActualHeight >= 560;
        BoardLayout.ColumnSpacing = sideInspector ? 24 : 0;
        RailColumn.Width = new GridLength(sideInspector ? 320 : 0);
        Grid.SetColumn(InspectorScroll, sideInspector ? 1 : 0);
        Grid.SetRow(InspectorScroll, sideInspector ? 0 : 1);
        var columns = ActualWidth - 48 - (sideInspector ? 344 : 0) >= 720;
        NotesLayout.ColumnSpacing = columns ? 16 : 0;
        PreparingColumn.Width = ReadyColumn.Width = columns ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(PreparingPanel, columns ? 1 : 0);
        Grid.SetRow(PreparingPanel, columns ? 0 : 1);
        Grid.SetColumn(ReadyPanel, columns ? 2 : 0);
        Grid.SetRow(ReadyPanel, columns ? 0 : 2);
        var headerInline = ActualWidth >= 760;
        HeaderLayout.ColumnSpacing = headerInline ? 16 : 0;
        HeaderActionColumn.Width = headerInline ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(HeaderActions, headerInline ? 1 : 0);
        Grid.SetRow(HeaderActions, headerInline ? 0 : 1);
        HeaderActions.Orientation = ActualWidth >= 560 ? Orientation.Horizontal : Orientation.Vertical;
        var height = Math.Max(320, ActualHeight - HeaderLayout.ActualHeight - 110 - (Status.IsOpen ? Status.ActualHeight : 0));
        InspectorScroll.MaxHeight = sideInspector ? height : double.PositiveInfinity;
        foreach (var list in new[] { ConfirmedList, PreparingList, ReadyList })
            list.MaxHeight = sideInspector ? Math.Max(220, height - 82) : double.PositiveInfinity;
    }

    private void OnShowPayment(object sender, RoutedEventArgs args)
    {
        PaymentSection.IsExpanded = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            PaymentAmountBox.StartBringIntoView();
            PaymentAmountBox.Focus(FocusState.Programmatic);
            PaymentAmountBox.SelectAll();
        });
    }

    private async void OnCardAction(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: ServiceBoardCard card } button) return;
        var keyboard = button.FocusState == FocusState.Keyboard;
        if (card.Order.Status == "ready" && card.Order.OutstandingBalance > 0)
        {
            model.Select(card.Order);
            OnShowPayment(sender, args);
            return;
        }
        await MoveCardAsync(card.Order.Id, card.NextStatus);
        if (keyboard && model.Error is null)
            DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var list in new[] { ConfirmedList, PreparingList, ReadyList })
                    if (list.Items.OfType<ServiceBoardCard>().FirstOrDefault(row => row.Order.Id == card.Order.Id) is { } moved)
                    {
                        list.ScrollIntoView(moved);
                        list.UpdateLayout();
                        (list.ContainerFromItem(moved) as Control)?.Focus(FocusState.Keyboard);
                        return;
                    }
                OperatorBox.Focus(FocusState.Keyboard);
            });
    }

    private async Task MoveCardAsync(EntityId id, string target)
    {
        await model.MoveOrderAsync(id, target);
        if (model.Error is null && target == "completed")
            await App.Services.GetRequiredService<CustomerWorkspace>().LoadAsync();
    }

    private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs args)
    {
        if (model.IsBusy || !model.HasOperator || args.Items.Count != 1 || args.Items[0] is not ServiceBoardCard card || card.Order.Status == "ready")
        {
            args.Cancel = true;
            return;
        }
        draggedOrderId = card.Order.Id;
        args.Data.SetData("CoffeeShop.ServiceOrder", card.Order.Id.ToString());
        args.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) => draggedOrderId = null;

    private void OnColumnDragOver(object sender, DragEventArgs args)
    {
        var allowed = sender is FrameworkElement { Tag: string target } && draggedOrderId is { } id && model.CanMoveOrder(id, target);
        args.AcceptedOperation = allowed ? DataPackageOperation.Move : DataPackageOperation.None;
        args.DragUIOverride.Caption = allowed ? $"Move to {((FrameworkElement)sender).Tag}" : "Use the next available state";
        args.DragUIOverride.IsCaptionVisible = true;
        args.Handled = true;
    }

    private async void OnColumnDrop(object sender, DragEventArgs args)
    {
        args.Handled = true;
        if (sender is not FrameworkElement { Tag: string target } || draggedOrderId is not { } id || !model.CanMoveOrder(id, target)) return;
        var deferral = args.GetDeferral();
        try
        {
            await MoveCardAsync(id, target);
            args.AcceptedOperation = model.Error is null ? DataPackageOperation.Move : DataPackageOperation.None;
        }
        finally { draggedOrderId = null; deferral.Complete(); }
    }
    private void OnDeliveryStaffChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering) AssignButton.IsEnabled = model.SelectedOrder?.CanAssignDelivery == true && DeliveryStaffBox.SelectedItem is DeliveryStaffOption && model.HasOperator && !model.IsBusy;
    }
    private void OnDiscountChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering) return;
        var enabled = DiscountList.SelectedItem is ServiceBoardDiscount && model.HasOperator && !model.IsBusy;
        ApproveButton.IsEnabled = RejectButton.IsEnabled = enabled;
    }
    private async void OnPrepare(object sender, RoutedEventArgs args) => await model.StartPreparingAsync();
    private async void OnReady(object sender, RoutedEventArgs args) => await model.MarkReadyAsync();
    private async void OnComplete(object sender, RoutedEventArgs args)
    {
        await model.CompleteAsync();
        if (model.Error is null) await App.Services.GetRequiredService<CustomerWorkspace>().LoadAsync();
    }
    private async void OnPay(object sender, RoutedEventArgs args)
    {
        if (decimal.TryParse(PaymentAmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            && amount > 0 && PaymentMethodBox.SelectedItem is ComboBoxItem method)
            await model.RecordPaymentAsync((string)method.Tag, amount, PaymentReferenceBox.Text);
        else model.ShowValidation("Enter a positive payment amount.");
    }
    private async void OnCancel(object sender, RoutedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(CancelReasonBox.Text))
        {
            CancelSection.IsExpanded = true;
            CancelReasonBox.Focus(FocusState.Programmatic);
            model.ShowValidation("Enter a cancellation reason under Cancel order.");
            return;
        }
        await model.CancelAsync(CancelReasonBox.Text);
    }
    private async void OnApprove(object sender, RoutedEventArgs args)
    {
        if (DiscountList.SelectedItem is ServiceBoardDiscount request) await model.DecideDiscountAsync(request.Id, true);
    }
    private async void OnReject(object sender, RoutedEventArgs args)
    {
        if (DiscountList.SelectedItem is ServiceBoardDiscount request) await model.DecideDiscountAsync(request.Id, false);
    }
    private async void OnAssign(object sender, RoutedEventArgs args)
    {
        if (DeliveryStaffBox.SelectedItem is DeliveryStaffOption staff) await model.AssignDeliveryAsync(staff.Id);
    }
    private async void OnOut(object sender, RoutedEventArgs args) => await model.StartDeliveryAsync();
    private async void OnDelivered(object sender, RoutedEventArgs args) => await model.CompleteDeliveryAsync();
    private async void OnFail(object sender, RoutedEventArgs args) => await model.FailDeliveryAsync(FailureReasonBox.Text);

    private void Render()
    {
        rendering = true;
        var active = model.Orders.Where(order => order.Status is "confirmed" or "preparing" or "ready").ToArray();
        var cards = active.Select(order => new ServiceBoardCard(order,
            model.OrderDetails.GetValueOrDefault(order.Id), order.Id == model.SelectedOrderId, model.HasOperator && !model.IsBusy)).ToArray();
        var selected = model.SelectedOrder;
        var detail = model.SelectedDetail;

        LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        OperatorBox.IsEnabled = !model.IsBusy;
        RefreshButton.IsEnabled = !model.IsBusy;
        OperatorHint.Visibility = model.HasOperator ? Visibility.Collapsed : Visibility.Visible;
        if (!ReferenceEquals(OperatorBox.ItemsSource, model.Staff)) OperatorBox.ItemsSource = model.Staff;
        OperatorBox.SelectedItem = model.Staff.FirstOrDefault(staff => staff.Id == model.Operator?.Id);
        foreach (var list in new[] { ConfirmedList, PreparingList, ReadyList }) list.CanDragItems = list != ReadyList && model.HasOperator && !model.IsBusy;
        Status.Apply(model.Feedback);
        if (model.Error is null && (model.Message.StartsWith("Choose an order") || model.Message.StartsWith("Load operational"))) Status.IsOpen = false;
        SummaryText.Text = active.Length == 0 ? "No active service work." :
            $"{active.Length} active tickets · Select a note to manage its order";
        EmptyText.Visibility = !model.IsBusy && active.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmedCount.Text = cards.Count(card => card.Status == "Confirmed").ToString(CultureInfo.CurrentCulture);
        PreparingCount.Text = cards.Count(card => card.Status == "Preparing").ToString(CultureInfo.CurrentCulture);
        ReadyCount.Text = cards.Count(card => card.Status == "Ready").ToString(CultureInfo.CurrentCulture);
        SetCards(ConfirmedList, cards.Where(card => card.Status == "Confirmed").ToArray());
        SetCards(PreparingList, cards.Where(card => card.Status == "Preparing").ToArray());
        SetCards(ReadyList, cards.Where(card => card.Status == "Ready").ToArray());
        ConfirmedEmpty.Visibility = cards.Any(card => card.Status == "Confirmed") ? Visibility.Collapsed : Visibility.Visible;
        PreparingEmpty.Visibility = cards.Any(card => card.Status == "Preparing") ? Visibility.Collapsed : Visibility.Visible;
        ReadyEmpty.Visibility = cards.Any(card => card.Status == "Ready") ? Visibility.Collapsed : Visibility.Visible;

        SelectedKicker.Text = selected is null ? "TICKET DETAILS" : $"TICKET {selected.OrderNumber}";
        SelectedTitle.Text = selected is null ? "Select a ticket" : selected.TableNumber is { } table ? $"Table {table}" : Humanize(selected.Channel);
        SelectedState.Text = selected is null ? "" : $"{Humanize(selected.Status)} · {Humanize(selected.Channel)}";
        SelectedCustomer.Text = detail?.CustomerName ?? (selected is null ? "" : "Walk-in");
        TicketBody.Visibility = TicketForms.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
        InspectorEmpty.Visibility = selected is null ? Visibility.Visible : Visibility.Collapsed;
        OrderLinesList.ItemsSource = detail?.Lines.Select(line => new ServiceBoardLine(line)).ToArray();
        TotalText.Text = selected is null ? "" : selected.Total.ToString("0.00", CultureInfo.CurrentCulture);
        BalanceText.Text = selected is null ? "" : $"Paid {selected.PaidAmount:0.00} · Due {selected.OutstandingBalance:0.00}";
        ActionHint.Text = selected is null ? "" : !model.HasOperator ? "Choose an operator to manage this ticket."
            : selected.Status == "ready" && selected.OutstandingBalance > 0 ? "Collect the balance before completing this order."
            : selected.Status == "ready" ? "Payment received. This ticket is ready to complete."
            : selected.Status == "confirmed" ? "Send this ticket to the bar to begin preparation." : "Mark ready when every item is prepared.";
        PaymentDueText.Text = selected?.OutstandingBalance > 0 ? $"Balance to collect: {selected.OutstandingBalance:0.00}" : "Paid in full";
        PaymentSection.Visibility = selected?.OutstandingBalance > 0 ? Visibility.Visible : Visibility.Collapsed;
        DiscountSection.Visibility = selected?.PendingDiscountIds.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        DeliverySection.Visibility = selected?.Channel == "delivery" ? Visibility.Visible : Visibility.Collapsed;
        CancelSection.Visibility = selected?.CanCancel == true ? Visibility.Visible : Visibility.Collapsed;
        DeliverySummary.Text = $"Status: {Humanize(selected?.DeliveryStatus ?? "unassigned")}" +
            (selected?.Status is "confirmed" or "preparing" ? "\nAssign a courier when this ticket is ready." : "");
        DeliveryAddress.Text = string.Join("\n", new[] { detail?.RecipientName, detail?.ContactNumber, detail?.AddressLine }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var orderChanged = formOrderId != selected?.Id;
        if (orderChanged)
        {
            formOrderId = selected?.Id;
            PaymentReferenceBox.Text = CancelReasonBox.Text = FailureReasonBox.Text = "";
            PaymentMethodBox.SelectedIndex = 0;
            PaymentSection.IsExpanded = CancelSection.IsExpanded = DeliverySection.IsExpanded = false;
            InspectorScroll.ChangeView(null, 0, null, true);
        }
        if (orderChanged || formBalance != selected?.OutstandingBalance || PaymentAmountBox.Text.Length == 0)
            PaymentAmountBox.Text = selected?.OutstandingBalance.ToString("0.00", CultureInfo.CurrentCulture) ?? "";
        formBalance = selected?.OutstandingBalance;
        var selectedDiscount = (DiscountList.SelectedItem as ServiceBoardDiscount)?.Id;
        DiscountList.ItemsSource = selected?.PendingDiscountIds.Select(id =>
        {
            var discount = detail?.Discounts.FirstOrDefault(row => row.Id == id);
            return new ServiceBoardDiscount(id, discount is null ? "Pending discount request" :
                discount.Type == "percentage" ? $"{discount.Value:0.##}% discount" : $"{discount.Value:0.00} discount");
        }).ToArray();
        DiscountList.SelectedItem = DiscountList.Items.OfType<ServiceBoardDiscount>().FirstOrDefault(row => row.Id == selectedDiscount);
        var selectedStaffId = orderChanged ? selected?.AssignedStaffId : (DeliveryStaffBox.SelectedItem as DeliveryStaffOption)?.Id;
        DeliveryStaffBox.ItemsSource = model.DeliveryStaff;
        DeliveryStaffBox.SelectedItem = model.DeliveryStaff.FirstOrDefault(staff => staff.Id == selectedStaffId);
        PrepareButton.Visibility = selected?.CanStartPreparing == true ? Visibility.Visible : Visibility.Collapsed;
        ReadyButton.Visibility = selected?.CanMarkReady == true ? Visibility.Visible : Visibility.Collapsed;
        CompleteButton.Visibility = selected?.CanComplete == true ? Visibility.Visible : Visibility.Collapsed;
        ReviewPaymentButton.Visibility = selected?.Status == "ready" && selected.OutstandingBalance > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReviewPaymentButton.IsEnabled = model.HasOperator && !model.IsBusy;
        PrepareButton.IsEnabled = selected?.CanStartPreparing == true && model.HasOperator && !model.IsBusy;
        ReadyButton.IsEnabled = selected?.CanMarkReady == true && model.HasOperator && !model.IsBusy;
        CompleteButton.IsEnabled = selected?.CanComplete == true && model.HasOperator && !model.IsBusy;
        PayButton.IsEnabled = selected?.CanRecordPayment == true && selected.OutstandingBalance > 0 && model.HasOperator && !model.IsBusy;
        CancelButton.IsEnabled = selected?.CanCancel == true && model.HasOperator && !model.IsBusy;
        ApproveButton.IsEnabled = RejectButton.IsEnabled = DiscountList.SelectedItem is ServiceBoardDiscount && model.HasOperator && !model.IsBusy;
        AssignButton.IsEnabled = selected?.CanAssignDelivery == true && DeliveryStaffBox.SelectedItem is DeliveryStaffOption && model.HasOperator && !model.IsBusy;
        OutButton.IsEnabled = selected?.CanStartDelivery == true && model.HasOperator && !model.IsBusy;
        DeliveredButton.IsEnabled = FailButton.IsEnabled = selected?.CanFinishDelivery == true && model.HasOperator && !model.IsBusy;
        DeliveryStaffBox.Visibility = AssignButton.Visibility = selected?.CanAssignDelivery == true ? Visibility.Visible : Visibility.Collapsed;
        OutButton.Visibility = selected?.CanStartDelivery == true ? Visibility.Visible : Visibility.Collapsed;
        DeliveredButton.Visibility = DeliveryFailureForm.Visibility = selected?.CanFinishDelivery == true ? Visibility.Visible : Visibility.Collapsed;
        rendering = false;
        ApplyBoardLayout();
    }

    private static string Humanize(string value) => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.Replace('_', ' ')).Replace("Dine In", "Dine-in");

    private static void SetCards(ListView list, ServiceBoardCard[] cards)
    {
        list.ItemsSource = cards;
        list.SelectedItem = cards.FirstOrDefault(card => card.IsSelected);
    }

    internal object VerificationLayout() => new
    {
        width = ActualWidth,
        inspectorAtSide = Grid.GetColumn(InspectorScroll) == 1,
        columns = Grid.GetColumn(PreparingPanel) == 1 ? 3 : 1,
        noteColumnWidth = ConfirmedPanel.ActualWidth,
        contentFits = BoardLayout.ActualWidth <= ActualWidth - 47,
        inspectorFits = Grid.GetColumn(InspectorScroll) == 0 || InspectorScroll.ActualHeight <= InspectorScroll.MaxHeight + 1
    };

    internal async Task<object> VerifyBoardAsync()
    {
        for (var attempt = 0; model.IsBusy && attempt < 100; attempt++) await Task.Delay(50);
        if (!model.IsBusy) await model.LoadAsync();
        verificationOperator = model.Operator;
        model.Operator = null;
        var withoutOperatorDisabled = new[] { ConfirmedList, PreparingList, ReadyList }
            .SelectMany(list => list.Items.OfType<ServiceBoardCard>()).All(card => !card.ActionEnabled);
        model.Operator = verificationOperator ?? model.Staff.FirstOrDefault();
        await Task.Delay(150);
        var headings = new[] { ConfirmedHeader, PreparingHeader, ReadyHeader };
        var lists = new[] { ConfirmedList, PreparingList, ReadyList };
        NotesLayout.UpdateLayout();
        var positions = headings.Select(header => header.TransformToVisual(NotesLayout).TransformPoint(new Windows.Foundation.Point())).ToArray();
        var aligned = Grid.GetColumn(PreparingPanel) == 1
            ? positions.Max(point => point.Y) - positions.Min(point => point.Y) < 1
            : positions.Max(point => point.X) - positions.Min(point => point.X) < 1;
        var measurements = lists.Select((list, index) =>
        {
            list.UpdateLayout();
            var paper = list.Items.Count == 0 ? null : FindPaper(list.ContainerFromIndex(0));
            double? paperX = paper?.TransformToVisual(NotesLayout).TransformPoint(new Windows.Foundation.Point()).X;
            return new { column = (string)((FrameworkElement)list.Parent).Tag, titleX = positions[index].X,
                paperX, matchesPaper = paperX is null ? (bool?)null : Math.Abs(positions[index].X - paperX.Value) < 1,
                notes = list.Items.Count, dragEnabled = list.CanDragItems };
        }).ToArray();
        var originalSelection = model.SelectedOrder;
        object? forms = null;
        try
        {
            var unpaid = model.Orders.FirstOrDefault(order => order.Status == "ready" && order.OutstandingBalance > 0);
            if (unpaid is not null)
            {
                model.Select(unpaid);
                OnShowPayment(this, new RoutedEventArgs());
                await Task.Delay(350);
                var paymentOpens = PaymentSection.IsExpanded && PaymentSection.Visibility == Visibility.Visible
                    && PaymentAmountBox.Text == unpaid.OutstandingBalance.ToString("0.00", CultureInfo.CurrentCulture)
                    && ReviewPaymentButton.Visibility == Visibility.Visible && CompleteButton.Visibility == Visibility.Collapsed;
                await PresentationVerification.CaptureAsync(PaymentSection, "native-service-board-payment.png");
                PaymentReferenceBox.Text = CancelReasonBox.Text = FailureReasonBox.Text = "verification draft";
                PaymentAmountBox.Text = "0.01";
                var other = model.Orders.FirstOrDefault(order => order.Id != unpaid.Id && order.Status is "confirmed" or "preparing" or "ready");
                bool? formsReset = null;
                if (other is not null)
                {
                    model.Select(other);
                    formsReset = PaymentReferenceBox.Text.Length == 0 && CancelReasonBox.Text.Length == 0 && FailureReasonBox.Text.Length == 0
                        && PaymentAmountBox.Text == other.OutstandingBalance.ToString("0.00", CultureInfo.CurrentCulture);
                }
                var delivery = model.Orders.FirstOrDefault(order => order.Channel == "delivery" && order.Status is "confirmed" or "preparing" or "ready");
                bool? deliveryIsContextual = null;
                if (delivery is not null)
                {
                    model.Select(delivery);
                    DeliverySection.IsExpanded = true;
                    DeliverySection.StartBringIntoView();
                    await Task.Delay(350);
                    deliveryIsContextual = DeliverySection.Visibility == Visibility.Visible
                        && AssignButton.Visibility == (delivery.CanAssignDelivery ? Visibility.Visible : Visibility.Collapsed)
                        && OutButton.Visibility == (delivery.CanStartDelivery ? Visibility.Visible : Visibility.Collapsed);
                    await PresentationVerification.CaptureAsync(DeliverySection, "native-service-board-delivery.png");
                }
                forms = new { paymentOpens, formsReset, deliveryIsContextual };
            }
        }
        finally
        {
            if (originalSelection is not null) model.Select(originalSelection);
            PaymentSection.IsExpanded = DeliverySection.IsExpanded = CancelSection.IsExpanded = false;
            InspectorScroll.ChangeView(null, 0, null, true);
            await Task.Delay(120);
        }
        return new { headersAligned = aligned, withoutOperatorDisabled, measurements, forms,
            moveButtons = lists.SelectMany(list => list.Items.OfType<ServiceBoardCard>()).Select(card => new
            { card.Number, card.Status, card.ActionLabel, card.ActionEnabled, card.NextStatus }).ToArray() };
    }

    internal void RestoreVerificationOperator() => model.Operator = verificationOperator;

    private static FrameworkElement? FindPaper(DependencyObject? root)
    {
        if (root is null) return null;
        if (root is FrameworkElement { Name: "Paper" } paper) return paper;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            if (FindPaper(VisualTreeHelper.GetChild(root, index)) is { } found) return found;
        return null;
    }
}
