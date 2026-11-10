using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

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

    internal object VerificationLayout() => new
    {
        twoColumns = Grid.GetColumn(BestPanel) == 1 && Grid.GetColumn(StockPanel) == 1,
        fourSummaryCards = Grid.GetColumn(AverageKpi) == 2,
        summaryFits = Kpis.ActualWidth <= ActualWidth - 47,
        dashboardFits = Dashboard.ActualWidth <= ActualWidth - 47,
        revenueFits = RevenuePanel.ActualWidth <= ActualWidth - 47,
        paymentsFit = ChannelPanel.ActualWidth <= ActualWidth - 47,
        chartDays = RevenueChart.ColumnDefinitions.Count,
        expectedDays = model.Revenue.Value?.Daily.Count
    };

    internal async Task<object> VerifyReportInteractionsAsync()
    {
        var first = model.FirstDate;
        var last = model.LastDate;
        var revenue = model.Revenue.Value?.Revenue;
        var stock = model.LowStock.Value?.Count;
        PaymentFilter.SelectedIndex = 1;
        ChannelFilter.SelectedIndex = 2;
        var filtersWork = RevenuePaymentsList.ItemsSource is IEnumerable<PaymentChartRow> payments && payments.All(row => row.Name == "Cash")
            && ChannelsGrid.Children.Count == 1 && model.Revenue.Value?.Revenue == revenue;
        ReportScroll.ChangeView(null, ReportScroll.ScrollableHeight, null, true);
        await Task.Delay(250);
        await PresentationVerification.CaptureAsync(this, "native-reports-breakdowns.png");
        FiltersFlyout.ShowAt(FiltersButton);
        await Task.Delay(250);
        await CaptureFlyoutAsync(FiltersFlyout, "native-reports-filters.png");
        FiltersFlyout.Hide();
        OnResetFilters(this, new RoutedEventArgs());
        var resetWorks = PaymentFilter.SelectedIndex == 0 && ChannelFilter.SelectedIndex == 0 && !ResetFiltersButton.IsEnabled;
        ReportScroll.ChangeView(null, 0, null, true);
        var dailyDetailsWork = false;
        if (DailyDetailsButton.Flyout is Flyout details)
        {
            details.ShowAt(DailyDetailsButton);
            await Task.Delay(250);
            dailyDetailsWork = RevenueDaysList.Items.Count == model.Revenue.Value?.Daily.Count;
            await CaptureFlyoutAsync(details, "native-reports-daily-details.png");
            details.Hide();
        }

        PeriodFlyout.ShowAt(PeriodButton);
        await Task.Delay(250);
        FirstDate.Date = AtLocalDate(last.AddDays(1));
        LastDate.Date = AtLocalDate(last);
        var invalidRejected = !await ApplyPeriodAsync() && PeriodError.IsOpen && model.FirstDate == first && model.LastDate == last;
        await Task.Delay(200);
        await CaptureFlyoutAsync(PeriodFlyout, "native-reports-period-validation.png");
        PeriodFlyout.Hide();
        FirstDate.Date = AtLocalDate(first.AddDays(1));
        await RefreshAsync();
        var refreshKeepsAppliedPeriod = model.FirstDate == first && model.LastDate == last;
        var future = last.AddYears(5);
        FirstDate.Date = LastDate.Date = AtLocalDate(future);
        await ApplyPeriodAsync();
        var emptyPeriodWorks = model.Revenue.Value is { OrderCount: 0 } && RevenueEmpty.Visibility == Visibility.Visible && RevenueChartScroll.Visibility == Visibility.Collapsed;
        var stockIndependentOfPeriod = model.LowStock.Value?.Count == stock;
        await PresentationVerification.CaptureAsync(this, "native-reports-empty.png");
        FirstDate.Date = AtLocalDate(first);
        LastDate.Date = AtLocalDate(last);
        await ApplyPeriodAsync();
        showRefreshFeedback = false;
        Render();
        ReportScroll.ChangeView(null, 0, null, true);
        return new { filtersWork, resetWorks, dailyDetailsWork, invalidRejected, refreshKeepsAppliedPeriod, emptyPeriodWorks, stockIndependentOfPeriod };
    }

    private static async Task CaptureFlyoutAsync(Flyout flyout, string fileName)
    {
        var surface = (Panel)flyout.Content;
        var background = surface.Background;
        surface.Background = ResourceBrush("ConsoleGroundBrush");
        try
        {
            await PresentationVerification.CaptureAsync(surface, fileName);
        }
        finally { surface.Background = background; }
    }

    private readonly ReportsWorkspace model;
    private bool showRefreshFeedback;
    public sealed record SellerChartRow(BestSellerViewRow Row, long Maximum)
    {
        public string Rank => Row.Rank.ToString("00");
        public string NameDisplay => Row.NameDisplay;
        public long Quantity => Row.Quantity;
        public string QuantityDisplay => $"{Quantity:N0} sold";
        public string RevenueDisplay => Row.RevenueDisplay;
    }
    public sealed record PaymentChartRow(string Name, string RevenueDisplay, string Percentage, Brush Color);

    public ReportsPage()
    {
        InitializeComponent();
        model = App.IsApiMode
            ? new ReportsWorkspace(App.Services.GetRequiredService<ApiReportsSource>())
            : new ReportsWorkspace(App.Services.GetRequiredService<IServiceScopeFactory>());
        model.PropertyChanged += (_, _) => Render();
        var today = DateTimeOffset.Now;
        FirstDate.Date = new DateTimeOffset(today.Year, today.Month, 1, 0, 0, 0, today.Offset);
        LastDate.Date = today;
        Loaded += async (_, _) => await LoadAsync();
    }

    private Task LoadAsync() => model.LoadAsync(FirstDate.Date, LastDate.Date, TimeZoneInfo.Local);
    private async void OnRefresh(object sender, RoutedEventArgs args) => await RefreshAsync();
    private async Task RefreshAsync()
    {
        showRefreshFeedback = true;
        if (model.FirstDate == default) await LoadAsync();
        else await model.LoadAsync(AtLocalDate(model.FirstDate), AtLocalDate(model.LastDate), TimeZoneInfo.Local);
    }

    private static DateTimeOffset AtLocalDate(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local));

    private void OnPeriodOpened(object sender, object args)
    {
        PeriodError.IsOpen = false;
        if (model.FirstDate == default) return;
        FirstDate.Date = AtLocalDate(model.FirstDate);
        LastDate.Date = AtLocalDate(model.LastDate);
    }

    private async void OnApplyPeriod(object sender, RoutedEventArgs args) => await ApplyPeriodAsync();
    private async Task<bool> ApplyPeriodAsync()
    {
        if (!ReportPresentation.TryCreatePeriod(FirstDate.Date, LastDate.Date, out _, out _, out var message))
        {
            PeriodError.Message = message;
            PeriodError.IsOpen = true;
            return false;
        }
        showRefreshFeedback = true;
        PeriodError.IsOpen = false;
        PeriodFlyout.Hide();
        await LoadAsync();
        return true;
    }

    private void Render()
    {
        LoadingIndicator.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = PeriodButton.IsEnabled = ApplyPeriodButton.IsEnabled = FirstDate.IsEnabled = LastDate.IsEnabled = !model.IsBusy;
        Status.Apply(model.Feedback);
        if (!showRefreshFeedback && !model.Feedback.IsError) Status.IsOpen = false;
        PeriodLabel.Text = ReportPresentation.PeriodLabel(model.FirstDate, model.LastDate);
        PeriodButton.Content = $"{model.FirstDate:dd MMM} – {model.LastDate:dd MMM}";
        ToolTipService.SetToolTip(PeriodButton, $"{ReportPresentation.PeriodLabel(model.FirstDate, model.LastDate)} · {TimeZoneInfo.Local.DisplayName}");
        var revenue = model.Revenue.Value;
        RevenueTotal.Text = revenue?.Revenue.ToString("N2") ?? "—";
        RevenueOrderCount.Text = revenue?.OrderCount.ToString("N0") ?? "—";
        AverageTicket.Text = revenue is null ? "—" : (revenue.OrderCount == 0 ? 0 : revenue.Revenue / revenue.OrderCount).ToString("N2");
        RevenueDaysList.ItemsSource = revenue is null ? null : ReportPresentation.RevenueDays(revenue);
        RevenueSummary.Text = revenue is null ? "" : $"{revenue.Daily.Count} active days · {revenue.OrderCount} recognized orders";
        StockCount.Text = model.LowStock.Value?.Count.ToString() ?? "—";
        SetRegionMessage(RevenueError, model.Revenue.Error?.Description);
        SetRegionMessage(BestSellersError, model.BestSellers.Error?.Description);
        SetRegionMessage(ChannelsError, model.Channels.Error?.Description);
        SetRegionMessage(LowStockError, model.LowStock.Error?.Description);
        RevenueEmpty.Visibility = revenue is { OrderCount: 0 } ? Visibility.Visible : Visibility.Collapsed;
        BestSellersEmpty.Visibility = model.BestSellers.Value is { Items.Count: 0 } ? Visibility.Visible : Visibility.Collapsed;
        LowStockEmpty.Visibility = model.LowStock.Value is { Count: 0 } ? Visibility.Visible : Visibility.Collapsed;
        BestSellersList.ItemsSource = model.BestSellers.Value is { } best
            ? ReportPresentation.BestSellers(best).Select(row => new SellerChartRow(row, Math.Max(1, best.Items.Max(item => item.Quantity)))).ToArray() : null;
        LowStockList.ItemsSource = model.LowStock.Value is { } stock ? ReportPresentation.LowStock(stock) : null;
        ApplyPaymentFilter();
        ApplyChannelFilter();
        UpdateFilterSummary();
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
        RevenueChart.MinWidth = 0;
        RevenueChartHint.Visibility = Visibility.Collapsed;
        var days = model.Revenue.Value?.Daily;
        RevenueChartScroll.Visibility = days is { Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        DailyDetailsButton.IsEnabled = days is { Count: > 0 };
        if (days is null || days.Count == 0) return;
        RevenueChart.MinWidth = days.Count * 72;
        const double plotHeight = 142;
        var maximum = Math.Max(0, days.Max(day => day.Revenue));
        var minimum = Math.Min(0, days.Min(day => day.Revenue));
        var range = maximum - minimum;
        var zeroPosition = range == 0 ? plotHeight : (double)(maximum / range) * plotHeight;
        for (var i = 0; i <= 4; i++)
        {
            var rule = new Border { Height = 1, Background = ResourceBrush("ConsoleRuleBrush"), Opacity = .65, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 26 + plotHeight / 4 * i, 0, 0) };
            Grid.SetColumnSpan(rule, days.Count);
            RevenueChart.Children.Add(rule);
        }
        var baseline = new Border { Height = 1, Background = ResourceBrush("ConsoleAccentBrush"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 26 + zeroPosition, 0, 0) };
        Grid.SetColumnSpan(baseline, days.Count);
        RevenueChart.Children.Add(baseline);
        foreach (var day in days)
        {
            RevenueChart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var column = new Grid { Margin = new Thickness(4, 0, 4, 0) };
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plotHeight) });
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            var value = new TextBlock { Text = day.Revenue.ToString("N2"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var barHeight = range == 0 ? 0 : (double)(Math.Abs(day.Revenue) / range) * plotHeight;
            var plot = new Grid();
            var bar = new Border
            {
                Height = barHeight,
                MaxWidth = 34,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, day.Revenue >= 0 ? Math.Max(0, zeroPosition - barHeight) : zeroPosition, 0, 0),
                Background = ResourceBrush(day.Revenue >= 0 ? "ConsoleFieldBrush" : "ConsoleMutedInkBrush"),
                BorderBrush = ResourceBrush("ConsoleAccentBrush"),
                BorderThickness = new Thickness(0, barHeight > 0 ? Math.Min(3, barHeight) : 0, 0, 0)
            };
            plot.Children.Add(bar);
            Grid.SetRow(plot, 1);
            var date = new TextBlock { Text = day.Date.ToString("dd MMM"), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(date, 2);
            column.Children.Add(value);
            column.Children.Add(plot);
            column.Children.Add(date);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(column, $"{day.Date:dd MMM yyyy}: revenue {day.Revenue:N2}, {day.OrderCount} orders");
            ToolTipService.SetToolTip(column, $"{day.Date:dd MMM}: {day.Revenue:N2} · {day.OrderCount} orders");
            Grid.SetColumn(column, RevenueChart.ColumnDefinitions.Count - 1);
            RevenueChart.Children.Add(column);
        }
        UpdateChartHint();
    }

    private void OnChartSizeChanged(object sender, SizeChangedEventArgs args) => UpdateChartHint();
    private void UpdateChartHint()
    {
        if (RevenueChartHint is null) return;
        var days = RevenueChart.ColumnDefinitions.Count;
        RevenueChartHint.Text = $"Scroll horizontally to see all {days} days.";
        RevenueChartHint.Visibility = days > 0 && RevenueChart.MinWidth > RevenueChartScroll.ActualWidth + 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPaymentFilterChanged(object sender, SelectionChangedEventArgs args) { if (model is not null) { ApplyPaymentFilter(); UpdateFilterSummary(); } }
    private void OnChannelFilterChanged(object sender, SelectionChangedEventArgs args) { if (model is not null) { ApplyChannelFilter(); UpdateFilterSummary(); } }
    private void OnResetFilters(object sender, RoutedEventArgs args)
    {
        PaymentFilter.SelectedIndex = ChannelFilter.SelectedIndex = 0;
        UpdateFilterSummary();
    }
    private void UpdateFilterSummary()
    {
        var active = (PaymentFilter.SelectedIndex > 0 ? 1 : 0) + (ChannelFilter.SelectedIndex > 0 ? 1 : 0);
        FiltersButton.Content = active == 0 ? "Filters" : $"Filters ({active})";
        ResetFiltersButton.IsEnabled = active > 0;
        FilterSummary.Text = active == 0 ? "All payment methods · All channels"
            : $"{(PaymentFilter.SelectedItem as ComboBoxItem)?.Content} · {(ChannelFilter.SelectedItem as ComboBoxItem)?.Content}";
    }
    private void ApplyPaymentFilter()
    {
        var report = model.Revenue.Value;
        var rows = report is null ? null : ReportPresentation.RevenuePayments(report, SelectedTag(PaymentFilter));
        var total = rows?.Sum(row => row.Revenue) ?? 0;
        RevenuePaymentsList.ItemsSource = rows?.Select(row => new PaymentChartRow(
            row.PaymentMethod switch { "cash" => "Cash", "card" => "Card", "e_wallet" => "E-wallet", _ => row.PaymentMethod },
            row.RevenueDisplay, total == 0 ? "0%" : $"{row.Revenue / total:P1}", PaymentBrush(row.PaymentMethod))).ToArray();
        PaymentsEmpty.Visibility = rows is { Count: 0 } ? Visibility.Visible : Visibility.Collapsed;
        PaymentArcs.Children.Clear();
        var orderCount = string.IsNullOrWhiteSpace(SelectedTag(PaymentFilter)) ? report?.OrderCount : rows?.Sum(row => row.OrderCount);
        PaymentOrderCount.Text = orderCount?.ToString("N0") ?? "—";
        PaymentDonut.Visibility = total > 0 && rows!.All(row => row.Revenue >= 0) ? Visibility.Visible : Visibility.Collapsed;
        if (PaymentDonut.Visibility == Visibility.Collapsed) return;
        var outline = new Ellipse { Width = 118, Height = 118, Stroke = ResourceBrush("ConsoleRuleBrush"), StrokeThickness = 1 };
        Canvas.SetLeft(outline, 1);
        Canvas.SetTop(outline, 1);
        PaymentArcs.Children.Add(outline);
        var angle = -Math.PI / 2;
        foreach (var row in rows!)
        {
            if (row.Revenue <= 0) continue;
            var sweep = (double)(row.Revenue / total) * Math.PI * 2;
            var end = angle + Math.Min(sweep, Math.PI * 2 - .0001);
            var figure = new PathFigure { StartPoint = new Point(60 + 45 * Math.Cos(angle), 60 + 45 * Math.Sin(angle)), IsClosed = false };
            figure.Segments.Add(new ArcSegment { Point = new Point(60 + 45 * Math.Cos(end), 60 + 45 * Math.Sin(end)), Size = new Size(45, 45), SweepDirection = SweepDirection.Clockwise, IsLargeArc = sweep > Math.PI });
            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            PaymentArcs.Children.Add(new Microsoft.UI.Xaml.Shapes.Path { Data = geometry, Stroke = PaymentBrush(row.PaymentMethod), StrokeThickness = 9 });
            angle += sweep;
        }
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PaymentDonut, $"Payment revenue distribution, {orderCount} distinct recognized orders for the selected payment filter");
    }
    private void ApplyChannelFilter()
    {
        var report = model.Channels.Value;
        var rows = report is null ? null : ReportPresentation.Channels(report, SelectedTag(ChannelFilter));
        ChannelsGrid.Children.Clear();
        ChannelsGrid.ColumnDefinitions.Clear();
        foreach (var row in rows ?? [])
        {
            ChannelsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var stack = new StackPanel { Spacing = 2, Padding = new Thickness(10, 8, 10, 8), BorderBrush = ResourceBrush("ConsoleRuleBrush"), BorderThickness = new Thickness(0, 0, 1, 0) };
            stack.Children.Add(new TextBlock { Text = row.ChannelDisplay, FontSize = 12, Foreground = ResourceBrush("ConsoleMutedInkBrush") });
            stack.Children.Add(new TextBlock { Text = row.RevenueDisplay, FontFamily = (FontFamily)Microsoft.UI.Xaml.Application.Current.Resources["ConsoleSerifFont"], FontSize = 24 });
            stack.Children.Add(new TextBlock { Text = $"{row.OrderCount} orders", FontSize = 12 });
            Grid.SetColumn(stack, ChannelsGrid.ColumnDefinitions.Count - 1);
            ChannelsGrid.Children.Add(stack);
        }
        ChannelsEmpty.Visibility = rows is not null && rows.All(row => row.OrderCount == 0) ? Visibility.Visible : Visibility.Collapsed;
    }
    private static Brush ResourceBrush(string key) => (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
    private static Brush PaymentBrush(string method) => ResourceBrush(method switch { "cash" => "ConsoleAccentBrush", "card" => "ConsoleFieldBrush", _ => "ConsoleMutedInkBrush" });
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
        Dashboard.ColumnSpacing = wide ? 24 : 0;
        var headerInline = ActualWidth >= 720;
        HeaderOrnament.Visibility = ActualWidth >= 1000 ? Visibility.Visible : Visibility.Collapsed;
        Header.ColumnDefinitions[1].Width = new GridLength(ActualWidth >= 1000 ? 116 : 0);
        Header.ColumnDefinitions[2].Width = headerInline ? GridLength.Auto : new GridLength(0);
        Header.ColumnSpacing = headerInline ? 18 : 0;
        Grid.SetColumn(HeaderActions, headerInline ? 2 : 0);
        Grid.SetColumnSpan(HeaderActions, headerInline ? 1 : 3);
        Grid.SetRow(HeaderActions, headerInline ? 0 : 1);
        ReportsTitle.FontSize = headerInline ? 52 : 40;
    }
}
