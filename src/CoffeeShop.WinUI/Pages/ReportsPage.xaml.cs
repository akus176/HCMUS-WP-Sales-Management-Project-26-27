using CoffeeShop.Application.Inventory;
using CoffeeShop.Application.Reporting;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class ReportsPage : Page
{
    private readonly RevenueReportQueryHandler revenueHandler;
    private readonly BestSellerReportQueryHandler bestSellerHandler;
    private readonly OrderChannelReportQueryHandler channelHandler;
    private readonly IInventoryRepository inventoryRepository;
    private RevenueReport? revenueReport;
    private OrderChannelReport? channelReport;

    public ReportsPage()
    {
        InitializeComponent();
        revenueHandler = App.Services.GetRequiredService<RevenueReportQueryHandler>();
        bestSellerHandler = App.Services.GetRequiredService<BestSellerReportQueryHandler>();
        channelHandler = App.Services.GetRequiredService<OrderChannelReportQueryHandler>();
        inventoryRepository = App.Services.GetRequiredService<IInventoryRepository>();

        var today = DateTimeOffset.Now;
        FirstDate.Date = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, today.Offset);
        LastDate.Date = today;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!ReportPresentation.TryCreatePeriod(FirstDate.Date, LastDate.Date, out var first, out var last, out var validationMessage))
        {
            ShowStatus(InfoBarSeverity.Warning, validationMessage!);
            return;
        }

        SetLoading(true);
        try
        {
            var timeZone = TimeZoneInfo.Local;
            var revenue = revenueHandler.GetRevenueAsync(new(first, last, timeZone));
            var bestSellers = bestSellerHandler.GetBestSellersAsync(new(first, last, timeZone));
            var channels = channelHandler.GetChannelsAsync(new(first, last, timeZone));
            var ingredients = inventoryRepository.ListIngredientsAsync();
            await Task.WhenAll(revenue, bestSellers, channels, ingredients);

            if (!revenue.Result.IsSuccess || !bestSellers.Result.IsSuccess || !channels.Result.IsSuccess)
            {
                var error = revenue.Result.Error ?? bestSellers.Result.Error ?? channels.Result.Error;
                ShowStatus(InfoBarSeverity.Error, error?.Description ?? "Could not load the selected reports.");
                return;
            }

            revenueReport = revenue.Result.Value;
            channelReport = channels.Result.Value;
            PeriodLabel.Text = ReportPresentation.PeriodLabel(first, last);
            RevenueTotal.Text = revenueReport.Revenue.ToString("0.00");
            RevenueOrderCount.Text = revenueReport.OrderCount.ToString();
            RevenueDaysList.ItemsSource = ReportPresentation.RevenueDays(revenueReport);
            BestSellersList.ItemsSource = ReportPresentation.BestSellers(bestSellers.Result.Value);
            var lowStock = ReportPresentation.LowStock(LowStockReport.Build(ingredients.Result));
            LowStockList.ItemsSource = lowStock;
            BestSellersEmpty.Visibility = bestSellers.Result.Value.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LowStockEmpty.Visibility = lowStock.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            ApplyPaymentFilter();
            ApplyChannelFilter();
            ShowStatus(InfoBarSeverity.Success, "Reports refreshed from the selected reporting period.");
        }
        catch (Exception)
        {
            ShowStatus(InfoBarSeverity.Error, "Could not load reports from the database. Check the connection and try again.");
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
        Loading.IsActive = loading;
        Loading.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowStatus(InfoBarSeverity severity, string message)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
    }
}
