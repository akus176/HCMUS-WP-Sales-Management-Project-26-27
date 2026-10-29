using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class ReportsPage : Page
{
    internal object VerificationState() => new { first = FirstDate.Date, last = LastDate.Date, payment = SelectedTag(PaymentFilter), channel = SelectedTag(ChannelFilter), revenue = model.Revenue.Value?.Revenue };

    internal async Task<object> PrepareVerificationAsync()
    {
        while (model.IsBusy) await Task.Delay(50);
        await LoadAsync();
        return new { revenue = model.Revenue.Value?.Revenue, orders = model.Revenue.Value?.OrderCount, bestSellers = model.BestSellers.Value?.Items.Count, lowStock = model.LowStock.Value?.Count, errors = new[] { model.Revenue.Error?.Code, model.BestSellers.Error?.Code, model.Channels.Error?.Code, model.LowStock.Error?.Code } };
    }

    private readonly ReportsWorkspace model;

    public ReportsPage()
    {
        InitializeComponent();
        model = new ReportsWorkspace(App.Services.GetRequiredService<IServiceScopeFactory>());
        model.PropertyChanged += (_, _) => Render();
        var today = DateTimeOffset.Now;
        FirstDate.Date = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, today.Offset);
        LastDate.Date = today;
        Loaded += async (_, _) => await LoadAsync();
    }

    private Task LoadAsync() => model.LoadAsync(FirstDate.Date, LastDate.Date, TimeZoneInfo.Local);
    private async void OnRefresh(object sender, RoutedEventArgs args)
    {
        PeriodFlyout.Hide();
        await LoadAsync();
    }

    private void Render()
    {
        LoadingIndicator.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = PeriodButton.IsEnabled = FirstDate.IsEnabled = LastDate.IsEnabled = !model.IsBusy;
        Status.Apply(model.Feedback);
        PeriodLabel.Text = ReportPresentation.PeriodLabel(model.FirstDate, model.LastDate);
        PeriodButton.Content = $"{model.FirstDate:dd MMM} – {model.LastDate:dd MMM}";
        var revenue = model.Revenue.Value;
        RevenueTotal.Text = revenue?.Revenue.ToString("N2") ?? "—";
        RevenueOrderCount.Text = revenue?.OrderCount.ToString("N0") ?? "—";
        AverageTicket.Text = revenue is null ? "—" : (revenue.OrderCount == 0 ? 0 : revenue.Revenue / revenue.OrderCount).ToString("N2");
        RevenueDaysList.ItemsSource = revenue is null ? null : ReportPresentation.RevenueDays(revenue);
        RevenueSummary.Text = revenue is null ? "" : $"{revenue.Daily.Count} days with revenue · {revenue.OrderCount} distinct recognized orders";
        StockCount.Text = model.LowStock.Value?.Count.ToString() ?? "—";
        SetRegionMessage(RevenueError, model.Revenue.Error?.Description);
        SetRegionMessage(BestSellersError, model.BestSellers.Error?.Description);
        SetRegionMessage(ChannelsError, model.Channels.Error?.Description);
        SetRegionMessage(LowStockError, model.LowStock.Error?.Description);
        RevenueEmpty.Visibility = revenue is { OrderCount: 0 } ? Visibility.Visible : Visibility.Collapsed;
        BestSellersEmpty.Visibility = model.BestSellers.Value is { Items.Count: 0 } ? Visibility.Visible : Visibility.Collapsed;
        LowStockEmpty.Visibility = model.LowStock.Value is { Count: 0 } ? Visibility.Visible : Visibility.Collapsed;
        BestSellersList.ItemsSource = model.BestSellers.Value is { } best ? ReportPresentation.BestSellers(best) : null;
        LowStockList.ItemsSource = model.LowStock.Value is { } stock ? ReportPresentation.LowStock(stock) : null;
        ApplyPaymentFilter();
        ApplyChannelFilter();
        RenderChart();
    }

    private static void SetRegionMessage(TextBlock target, string? message)
    {
        target.Text = message ?? "";
        target.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RenderChart()
    {
        RevenueChart.Children.Clear();
        RevenueChart.ColumnDefinitions.Clear();
        var days = model.Revenue.Value?.Daily;
        if (days is null || days.Count == 0) return;
        var maximum = days.Max(day => day.Revenue);
        foreach (var day in days)
        {
            RevenueChart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Stretch, Spacing = 6, Margin = new Thickness(2, 0, 2, 0) };
            var value = new TextBlock { Text = day.Revenue.ToString("0.##"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center };
            var bar = new Border
            {
                Height = maximum == 0 ? 0 : (double)(day.Revenue / maximum) * 130,
                MaxWidth = 30,
                Background = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ConsoleFieldBrush"],
                BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ConsoleAccentBrush"],
                BorderThickness = new Thickness(0, 3, 0, 0)
            };
            var date = new TextBlock { Text = days.Count <= 8 ? day.Date.ToString("ddd dd") : day.Date.ToString("dd"), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(value);
            stack.Children.Add(bar);
            stack.Children.Add(date);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(stack, $"{day.Date:dd MMM yyyy}: revenue {day.Revenue:N2}, {day.OrderCount} orders");
            ToolTipService.SetToolTip(stack, $"{day.Date:dd MMM}: {day.Revenue:N2} · {day.OrderCount} orders");
            Grid.SetColumn(stack, RevenueChart.ColumnDefinitions.Count - 1);
            RevenueChart.Children.Add(stack);
        }
    }

    private void OnPaymentFilterChanged(object sender, SelectionChangedEventArgs args) { if (model is not null) ApplyPaymentFilter(); }
    private void OnChannelFilterChanged(object sender, SelectionChangedEventArgs args) { if (model is not null) ApplyChannelFilter(); }
    private void ApplyPaymentFilter()
    {
        var report = model.Revenue.Value;
        var rows = report is null ? null : ReportPresentation.RevenuePayments(report, SelectedTag(PaymentFilter));
        RevenuePaymentsList.ItemsSource = rows;
        PaymentsEmpty.Visibility = rows is { Count: 0 } ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ApplyChannelFilter()
    {
        var report = model.Channels.Value;
        var rows = report is null ? null : ReportPresentation.Channels(report, SelectedTag(ChannelFilter));
        ChannelsList.ItemsSource = rows;
        ChannelsEmpty.Visibility = rows is not null && rows.All(row => row.OrderCount == 0) ? Visibility.Visible : Visibility.Collapsed;
    }
    private static string? SelectedTag(ComboBox comboBox) => (comboBox.SelectedItem as ComboBoxItem)?.Tag as string;
    private void OnOpenStockRoom(object sender, RoutedEventArgs args) => (((App)Microsoft.UI.Xaml.Application.Current).Window as MainWindow)?.NavigateTo("lowstock");
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (RightColumn is null) return;
        var wide = ActualWidth >= 900;
        RightColumn.Width = wide ? new GridLength(.85, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(BestPanel, wide ? 1 : 0);
        Grid.SetRow(BestPanel, wide ? 0 : 1);
        Grid.SetRow(ChannelPanel, wide ? 1 : 2);
        Grid.SetColumn(StockPanel, wide ? 1 : 0);
        Grid.SetRow(StockPanel, wide ? 1 : 3);
        var fourKpis = ActualWidth >= 800;
        Kpis.ColumnDefinitions[2].Width = Kpis.ColumnDefinitions[3].Width = fourKpis ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(AverageKpi, fourKpis ? 2 : 0);
        Grid.SetColumn(StockKpi, fourKpis ? 3 : 1);
        Grid.SetRow(AverageKpi, fourKpis ? 0 : 1);
        Grid.SetRow(StockKpi, fourKpis ? 0 : 1);
        HeaderOrnament.Visibility = ActualWidth >= 850 ? Visibility.Visible : Visibility.Collapsed;
        Header.ColumnDefinitions[1].Width = new GridLength(ActualWidth >= 850 ? 116 : 0);
        HeaderActions.Orientation = ActualWidth >= 700 ? Orientation.Horizontal : Orientation.Vertical;
    }
}
