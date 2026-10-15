using System.ComponentModel;
using System.Globalization;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class OperationsPage : Page
{
    private readonly OperationsWorkspace model;
    private bool rendering;
    public OperationsPage() { InitializeComponent(); model = App.Services.GetRequiredService<OperationsWorkspace>(); Loaded += OnLoaded; Unloaded += OnUnloaded; }
    private async void OnLoaded(object sender, RoutedEventArgs args) { model.PropertyChanged += OnChanged; model.EnableOperationalWorkflow(); await model.LoadAsync(); }
    private void OnUnloaded(object sender, RoutedEventArgs args) => model.PropertyChanged -= OnChanged;
    private void OnChanged(object? sender, PropertyChangedEventArgs args) => Render();
    private async void OnReload(object sender, RoutedEventArgs args) => await model.LoadAsync();
    private void OnOrderChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering) return;
        var selected = sender is ListView list ? list.SelectedItem : sender is ComboBox box ? box.SelectedItem : null;
        model.Select(selected as OperationalOrder);
    }
    private async void OnPrepare(object sender, RoutedEventArgs args) => await model.StartPreparingAsync();
    private async void OnReady(object sender, RoutedEventArgs args) => await model.MarkReadyAsync();
    private async void OnComplete(object sender, RoutedEventArgs args) => await model.CompleteAsync();
    private async void OnPay(object sender, RoutedEventArgs args) { if (decimal.TryParse(PaymentAmountBox.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) && PaymentMethodBox.SelectedItem is ComboBoxItem method) await model.RecordPaymentAsync((string)method.Tag, amount, PaymentReferenceBox.Text); else model.ShowValidation("Enter a positive payment amount."); }
    private async void OnCancel(object sender, RoutedEventArgs args) => await model.CancelAsync(CancelReasonBox.Text);
    private async void OnApprove(object sender, RoutedEventArgs args) { if (DiscountList.SelectedItem is EntityId id) await model.DecideDiscountAsync(id, true); }
    private async void OnReject(object sender, RoutedEventArgs args) { if (DiscountList.SelectedItem is EntityId id) await model.DecideDiscountAsync(id, false); }
    private async void OnAssign(object sender, RoutedEventArgs args) { if (DeliveryStaffBox.SelectedItem is DeliveryStaffOption staff) await model.AssignDeliveryAsync(staff.Id); }
    private async void OnOut(object sender, RoutedEventArgs args) => await model.StartDeliveryAsync();
    private async void OnDelivered(object sender, RoutedEventArgs args) => await model.CompleteDeliveryAsync();
    private async void OnFail(object sender, RoutedEventArgs args) => await model.FailDeliveryAsync(FailureReasonBox.Text);
    private void Render()
    {
        rendering = true; var order = model.SelectedOrder;
        LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed; Status.Severity = model.Error is null ? InfoBarSeverity.Informational : InfoBarSeverity.Error; Status.Message = model.Message;
        QueueList.ItemsSource = model.Orders.Where(item => item.Status is "confirmed" or "preparing").ToArray(); TablesList.ItemsSource = model.Tables; OrderBox.ItemsSource = model.Orders; OrderBox.SelectedItem = order; DeliveryStaffBox.ItemsSource = model.DeliveryStaff;
        EmptyText.Visibility = model.IsEmpty ? Visibility.Visible : Visibility.Collapsed; OrderDetails.Text = order is null ? "Select an order to view state-specific controls." : $"{order.Status} / {order.Channel}\nTotal {order.Total:0.00} · paid {order.PaidAmount:0.00} · outstanding {order.OutstandingBalance:0.00}\nTable {order.TableNumber ?? "—"} · delivery {order.DeliveryStatus ?? "—"}";
        DiscountList.ItemsSource = order?.PendingDiscountIds ?? []; PrepareButton.IsEnabled = order?.CanStartPreparing == true && model.HasOperator; ReadyButton.IsEnabled = order?.CanMarkReady == true && model.HasOperator; CompleteButton.IsEnabled = order?.CanComplete == true && model.HasOperator; PayButton.IsEnabled = order?.CanRecordPayment == true && model.HasOperator; CancelButton.IsEnabled = order?.CanCancel == true && model.HasOperator; ApproveButton.IsEnabled = RejectButton.IsEnabled = order?.PendingDiscountIds.Count > 0 && model.HasOperator; AssignButton.IsEnabled = order?.CanAssignDelivery == true && DeliveryStaffBox.SelectedItem is not null && model.HasOperator; OutButton.IsEnabled = order?.CanStartDelivery == true && model.HasOperator; DeliveredButton.IsEnabled = FailButton.IsEnabled = order?.CanFinishDelivery == true && model.HasOperator;
        rendering = false;
    }
}
