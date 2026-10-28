using System.ComponentModel;
using System.Globalization;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed record ServiceBoardCard(OperationalOrder Order, PosOrderDetail? Detail, bool IsSelected)
{
    public string Status => char.ToUpperInvariant(Order.Status[0]) + Order.Status[1..];
    public string Number => Order.OrderNumber;
    public string ChannelLabel => Order.TableNumber is { } table ? $"T-{table}" : Order.Channel.ToUpperInvariant();
    public string Headline => Detail?.CustomerName ?? (Order.TableNumber is { } table ? $"Table {table}" : "Walk-in");
    public string Items => Detail is { Lines.Count: > 0 } detail
        ? string.Join(" · ", detail.Lines.Select(line => $"{(line.Quantity > 1 ? $"{line.Quantity}× " : "")}{line.ItemName}"))
        : "Order details available on selection";
    public string Footer => IsSelected ? "SELECTED" : Order.Channel.ToUpperInvariant();
    public string ActionLabel => Order.Status switch { "confirmed" => "START →", "preparing" => "FINISH →", "ready" => "SERVE →", _ => "" };
}

public sealed record ServiceBoardTable(string TableNumber, string Seats);

public sealed partial class OperationsPage : Page
{
    private readonly OperationsWorkspace model;
    private bool rendering;

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
    }
    private void OnDeliveryStaffChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering) AssignButton.IsEnabled = model.SelectedOrder?.CanAssignDelivery == true && DeliveryStaffBox.SelectedItem is DeliveryStaffOption && model.HasOperator;
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
            model.ShowValidation("Enter a cancellation reason under Cancellation and delivery.");
            return;
        }
        await model.CancelAsync(CancelReasonBox.Text);
    }
    private async void OnApprove(object sender, RoutedEventArgs args)
    {
        if (DiscountList.SelectedItem is EntityId id) await model.DecideDiscountAsync(id, true);
    }
    private async void OnReject(object sender, RoutedEventArgs args)
    {
        if (DiscountList.SelectedItem is EntityId id) await model.DecideDiscountAsync(id, false);
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
            model.OrderDetails.GetValueOrDefault(order.Id), order.Id == model.SelectedOrderId)).ToArray();
        var selected = model.SelectedOrder;
        var detail = model.SelectedDetail;

        LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        Status.Apply(model.Feedback);
        if (model.Error is null && (model.Message.StartsWith("Choose an order") || model.Message.StartsWith("Load operational"))) Status.IsOpen = false;
        SummaryText.Text = active.Length == 0 ? "No active service work." :
            $"{cards.Count(card => card.Status == "Preparing")} preparing, {cards.Count(card => card.Status == "Ready")} ready, {cards.Count(card => card.Status == "Confirmed")} confirmed.";
        EmptyText.Visibility = !model.IsBusy && active.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmedCount.Text = cards.Count(card => card.Status == "Confirmed").ToString(CultureInfo.CurrentCulture);
        PreparingCount.Text = cards.Count(card => card.Status == "Preparing").ToString(CultureInfo.CurrentCulture);
        ReadyCount.Text = cards.Count(card => card.Status == "Ready").ToString(CultureInfo.CurrentCulture);
        SetCards(ConfirmedList, cards.Where(card => card.Status == "Confirmed").ToArray());
        SetCards(PreparingList, cards.Where(card => card.Status == "Preparing").ToArray());
        SetCards(ReadyList, cards.Where(card => card.Status == "Ready").ToArray());
        TablesList.ItemsSource = model.Tables.Select(table => new ServiceBoardTable(table.TableNumber, $"{table.Seats?.ToString() ?? "—"} SEATS")).ToArray();

        SelectedKicker.Text = selected is null ? "SELECT A TICKET" : $"SELECTED · TICKET {selected.OrderNumber}";
        SelectedTitle.Text = selected is null ? "No ticket selected" : selected.TableNumber is { } table ? $"Table {table}" : detail?.CustomerName ?? "Walk-in";
        SelectedState.Text = selected is null ? "" : $"{selected.Status.ToUpperInvariant()}  ·  {selected.Channel.ToUpperInvariant()}";
        SelectedCustomer.Text = detail?.CustomerName ?? (selected is null ? "" : "Walk-in");
        OrderLinesList.ItemsSource = detail?.Lines;
        TotalText.Text = selected is null ? "" : selected.Total.ToString("0.00", CultureInfo.CurrentCulture);
        BalanceText.Text = selected is null ? "" : $"Paid {selected.PaidAmount:0.00} · Outstanding {selected.OutstandingBalance:0.00}" +
            (detail?.AddressLine is { } address ? $" · Delivery: {address}" : "");
        DiscountList.ItemsSource = selected?.PendingDiscountIds ?? [];
        DeliveryStaffBox.ItemsSource = model.DeliveryStaff;
        PrepareButton.Visibility = selected?.CanStartPreparing == true ? Visibility.Visible : Visibility.Collapsed;
        ReadyButton.Visibility = selected?.CanMarkReady == true ? Visibility.Visible : Visibility.Collapsed;
        CompleteButton.Visibility = selected?.CanComplete == true ? Visibility.Visible : Visibility.Collapsed;
        PrepareButton.IsEnabled = selected?.CanStartPreparing == true && model.HasOperator && !model.IsBusy;
        ReadyButton.IsEnabled = selected?.CanMarkReady == true && model.HasOperator && !model.IsBusy;
        CompleteButton.IsEnabled = selected?.CanComplete == true && model.HasOperator && !model.IsBusy;
        PayButton.IsEnabled = selected?.CanRecordPayment == true && selected.OutstandingBalance > 0 && model.HasOperator && !model.IsBusy;
        CancelButton.IsEnabled = selected?.CanCancel == true && model.HasOperator && !model.IsBusy;
        ApproveButton.IsEnabled = RejectButton.IsEnabled = selected?.PendingDiscountIds.Count > 0 && model.HasOperator && !model.IsBusy;
        AssignButton.IsEnabled = selected?.CanAssignDelivery == true && DeliveryStaffBox.SelectedItem is DeliveryStaffOption && model.HasOperator && !model.IsBusy;
        OutButton.IsEnabled = selected?.CanStartDelivery == true && model.HasOperator && !model.IsBusy;
        DeliveredButton.IsEnabled = FailButton.IsEnabled = selected?.CanFinishDelivery == true && model.HasOperator && !model.IsBusy;
        rendering = false;
    }

    private static void SetCards(ListView list, ServiceBoardCard[] cards)
    {
        list.ItemsSource = cards;
        list.SelectedItem = cards.FirstOrDefault(card => card.IsSelected);
    }
}
