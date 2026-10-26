using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class ReportsPage : Page
{
    private RevenueReport? revenueReport;
    private OrderChannelReport? channelReport;

    public ReportsPage()
    {
        InitializeComponent();
        var today = DateTimeOffset.Now;
        FirstDate.Date = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, today.Offset);
        LastDate.Date = today;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!ReportPresentation.TryCreatePeriod(FirstDate.Date, LastDate.Date, out var first, out var last, out var validationMessage))
        {
            Status.Apply(WorkflowFeedback.Validation("reporting period", validationMessage!));
            return;
        }

        SetLoading(true);
        Status.Apply(WorkflowFeedback.Loading("Loading reports for the selected period..."));
        try
        {
            using var scope = App.Services.CreateScope();
            var revenueHandler = scope.ServiceProvider.GetRequiredService<RevenueReportQueryHandler>();
            var bestSellerHandler = scope.ServiceProvider.GetRequiredService<BestSellerReportQueryHandler>();
            var channelHandler = scope.ServiceProvider.GetRequiredService<OrderChannelReportQueryHandler>();
            var inventoryRepository = scope.ServiceProvider.GetRequiredService<IInventoryRepository>();
            var timeZone = TimeZoneInfo.Local;
            // These queries share the scope's DbContext, which allows one operation at a time.
            var revenue = await revenueHandler.GetRevenueAsync(new(first, last, timeZone));
            var bestSellers = await bestSellerHandler.GetBestSellersAsync(new(first, last, timeZone));
            var channels = await channelHandler.GetChannelsAsync(new(first, last, timeZone));
            var ingredients = await inventoryRepository.ListIngredientsAsync();

            if (!revenue.IsSuccess || !bestSellers.IsSuccess || !channels.IsSuccess)
            {
                var error = revenue.Error ?? bestSellers.Error ?? channels.Error;
                Status.Apply(error is null
                    ? WorkflowFeedback.DatabaseFailure("load the selected reports", "Check the local database connection, then select Refresh reports.")
                    : WorkflowFeedback.FromError(error));
                return;
            }

            revenueReport = revenue.Value;
            channelReport = channels.Value;
            PeriodLabel.Text = ReportPresentation.PeriodLabel(first, last);
            RevenueTotal.Text = revenueReport.Revenue.ToString("0.00");
            RevenueOrderCount.Text = revenueReport.OrderCount.ToString();
            RevenueDaysList.ItemsSource = ReportPresentation.RevenueDays(revenueReport);
            BestSellersList.ItemsSource = ReportPresentation.BestSellers(bestSellers.Value);
            var lowStock = ReportPresentation.LowStock(LowStockReport.Build(ingredients));
            LowStockList.ItemsSource = lowStock;
            BestSellersEmpty.Visibility = bestSellers.Value.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LowStockEmpty.Visibility = lowStock.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ApplyPaymentFilter();
            ApplyChannelFilter();
            Status.Apply(WorkflowFeedback.Success("Reports refreshed from the selected reporting period."));
        }
        catch (Exception)
        {
            Status.Apply(WorkflowFeedback.DatabaseFailure(
                "load reports from the database",
                "Check the local database connection, then select Refresh reports."));
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void OnPaymentFilterChanged(object sender, SelectionChangedEventArgs args) => ApplyPaymentFilter();

    private void OnChannelFilterChanged(object sender, SelectionChangedEventArgs args) => ApplyChannelFilter();

    private void ApplyPaymentFilter()
    {
        if (revenueReport is null)
        {
            return;
        }

        var rows = ReportPresentation.RevenuePayments(revenueReport, SelectedTag(PaymentFilter));
        RevenuePaymentsList.ItemsSource = rows;
        RevenueEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyChannelFilter()
    {
        if (channelReport is null)
        {
            return;
        }

        var rows = ReportPresentation.Channels(channelReport, SelectedTag(ChannelFilter));
        ChannelsList.ItemsSource = rows;
        ChannelsEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string? SelectedTag(ComboBox comboBox) => (comboBox.SelectedItem as ComboBoxItem)?.Tag as string;

    private async void OnRefresh(object sender, RoutedEventArgs args) => await LoadAsync();

    private void SetLoading(bool loading)
    {
        LoadingIndicator.IsActive = loading;
        LoadingIndicator.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = !loading;
        FirstDate.IsEnabled = !loading;
        LastDate.IsEnabled = !loading;
        PaymentFilter.IsEnabled = !loading;
        ChannelFilter.IsEnabled = !loading;
    }
}
