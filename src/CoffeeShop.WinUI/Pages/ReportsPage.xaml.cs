using System.Text.Json;
using CoffeeShop.Contracts;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;

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
        revenueControlsFit = RevenueControls.ActualWidth <= RevenueHeader.ActualWidth + 1,
        paymentsFit = ChannelPanel.ActualWidth <= ActualWidth - 47,
        chartColumns = RevenueChart.ColumnDefinitions.Count,
        tableRows = RevenuePeriodsList.Items.Count
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
        FiltersButton.StartBringIntoView();
        await Task.Delay(250);
        FiltersFlyout.ShowAt(FiltersButton);
        await Task.Delay(250);
        await CaptureFlyoutAsync(FiltersFlyout, "native-reports-filters.png");
        FiltersFlyout.Hide();
        OnResetFilters(this, new RoutedEventArgs());
        var resetWorks = PaymentFilter.SelectedIndex == 0 && ChannelFilter.SelectedIndex == 0 && !ResetFiltersButton.IsEnabled;
        ReportScroll.ChangeView(null, 0, null, true);
        RevenueTableOption.IsChecked = true;
        UpdateLayout();
        var revenueTableWorks = RevenuePeriodsList.Items.Count == model.Revenue.Value?.Daily.Count
            && RevenueGraph.Visibility == Visibility.Collapsed;
        await PresentationVerification.CaptureAsync(this, "native-reports-revenue-table.png");
        var groupedRevenueWorks = true;
        foreach (var groupingOption in new[] { RevenueMonthOption, RevenueYearOption })
        {
            RevenueTableOption.IsChecked = true;
            groupingOption.IsChecked = true;
            var rowCount = RevenuePeriodsList.Items.Count;
            groupedRevenueWorks &= RevenuePeriodsList.Items.OfType<RevenuePeriodRow>().Sum(row => row.Revenue) == revenue
                && RevenueTable.Visibility == (rowCount > 0 ? Visibility.Visible : Visibility.Collapsed);
            RevenueChartOption.IsChecked = true;
            groupedRevenueWorks &= RevenueChart.ColumnDefinitions.Count == rowCount && RevenueTable.Visibility == Visibility.Collapsed
                && (rowCount == 0 || RevenueAxis.Children.Count >= 2);
        }
        RevenueDayOption.IsChecked = true;

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
        var emptyPeriodWorks = model.Revenue.Value is { OrderCount: 0 } && RevenueEmpty.Visibility == Visibility.Visible && RevenueGraph.Visibility == Visibility.Collapsed;
        var stockIndependentOfPeriod = model.LowStock.Value?.Count == stock;
        await PresentationVerification.CaptureAsync(this, "native-reports-empty.png");
        FirstDate.Date = AtLocalDate(first);
        LastDate.Date = AtLocalDate(last);
        await ApplyPeriodAsync();
        showRefreshFeedback = false;
        Render();
        ReportScroll.ChangeView(null, 0, null, true);
        return new { filtersWork, resetWorks, revenueTableWorks, groupedRevenueWorks, invalidRejected, refreshKeepsAppliedPeriod, emptyPeriodWorks, stockIndependentOfPeriod };
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
    private readonly ApiAssistantSource? assistant;
    private bool showRefreshFeedback;
    private bool askingAssistant;
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
        if (App.IsApiMode)
        {
            var session = App.Services.GetRequiredService<StaffSessionManager>().CurrentUser;
            if (session is not null && session.Permissions.Contains("assistant.ask") &&
                (session.Roles.Contains("Manager") || session.Roles.Contains("Admin")))
            {
                assistant = App.Services.GetRequiredService<ApiAssistantSource>();
                AssistantLauncherButton.Visibility = Visibility.Visible;
            }
        }
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
        AssistantPeriod.Text = model.FirstDate == default ? "Choose report dates" : $"{model.FirstDate:dd MMM yyyy} – {model.LastDate:dd MMM yyyy}";
        AssistantDatesButton.IsEnabled = !model.IsBusy;
        UpdateAssistantAction();
        PeriodButton.Content = $"{model.FirstDate:dd MMM} – {model.LastDate:dd MMM}";
        ToolTipService.SetToolTip(PeriodButton, $"{ReportPresentation.PeriodLabel(model.FirstDate, model.LastDate)} · {TimeZoneInfo.Local.DisplayName}");
        var revenue = model.Revenue.Value;
        RevenueTotal.Text = revenue?.Revenue.ToString("N2") ?? "—";
        RevenueOrderCount.Text = revenue?.OrderCount.ToString("N0") ?? "—";
        AverageTicket.Text = revenue is null ? "—" : (revenue.OrderCount == 0 ? 0 : revenue.Revenue / revenue.OrderCount).ToString("N2");
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
        RenderRevenueView();
    }

    private static void SetRegionMessage(TextBlock target, string? message)
    {
        target.Text = message ?? "";
        target.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private RevenueGrouping SelectedRevenueGrouping => RevenueMonthOption.IsChecked == true ? RevenueGrouping.Month
        : RevenueYearOption.IsChecked == true ? RevenueGrouping.Year : RevenueGrouping.Day;

    private void OnRevenueViewChanged(object sender, RoutedEventArgs args)
    {
        if (model is null) return;
        if (sender is RadioButton { GroupName: "RevenueGrouping" })
            RevenueChartScroll.ChangeView(0, null, null, true);
        RenderRevenueView();
    }

    private void OnRevenueHeaderSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (RevenueViewSegments is null) return;
        var inline = args.NewSize.Width >= 450;
        RevenueHeader.ColumnDefinitions[1].Width = inline ? GridLength.Auto : new GridLength(0);
        RevenueHeader.ColumnSpacing = inline ? 12 : 0;
        RevenueHeader.RowSpacing = inline ? 0 : 8;
        Grid.SetColumn(RevenueControls, inline ? 1 : 0);
        Grid.SetRow(RevenueControls, inline ? 0 : 1);
        RevenueControls.HorizontalAlignment = inline ? HorizontalAlignment.Right : HorizontalAlignment.Left;

        var stackControls = args.NewSize.Width < 280;
        RevenueControls.ColumnSpacing = stackControls ? 0 : 8;
        RevenueControls.RowSpacing = stackControls ? 6 : 0;
        Grid.SetColumn(RevenueViewSegments, stackControls ? 0 : 1);
        Grid.SetRow(RevenueViewSegments, stackControls ? 1 : 0);
    }

    private void RenderRevenueView()
    {
        var report = model.Revenue.Value;
        var grouping = SelectedRevenueGrouping;
        var periods = report is null ? [] : ReportPresentation.RevenuePeriods(report, grouping);
        var showTable = periods.Count > 0 && RevenueTableOption.IsChecked == true;
        var showChart = periods.Count > 0 && !showTable;
        RevenueTable.Visibility = showTable ? Visibility.Visible : Visibility.Collapsed;
        RevenuePeriodsList.ItemsSource = showTable ? periods : null;
        RevenueGraph.Visibility = showChart ? Visibility.Visible : Visibility.Collapsed;
        RevenueSummaryPanel.Visibility = periods.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var unit = grouping.ToString().ToLowerInvariant();
        RevenueSummary.Text = report is null ? "" :
            $"{periods.Count:N0} active {unit}{(periods.Count == 1 ? "" : "s")} · {report.OrderCount:N0} recognized orders in period";
        RenderChart(showChart ? periods : []);
    }

    private void RenderChart(IReadOnlyList<RevenuePeriodRow> periods)
    {
        RevenueAxis.Children.Clear();
        RevenueChart.Children.Clear();
        RevenueChart.ColumnDefinitions.Clear();
        RevenueChart.MinWidth = 0;
        RevenueChart.Width = double.NaN;
        RevenueChartHint.Visibility = Visibility.Collapsed;
        if (periods.Count == 0) return;
        var columnWidth = Math.Max(64, periods.Max(row => row.ChartLabel.Length) * 7 + 12);
        RevenueChart.MinWidth = periods.Count * columnWidth;
        const double plotTop = 12;
        const double plotHeight = 154;
        var minimum = Math.Min(0, (double)periods.Min(row => row.Revenue));
        var maximum = Math.Max(0, (double)periods.Max(row => row.Revenue));
        var tickStep = RevenueTickStep(maximum > minimum ? (maximum - minimum) / 4 : 1);
        var scaleMin = Math.Floor(minimum / tickStep) * tickStep;
        var scaleMax = Math.Ceiling(maximum / tickStep) * tickStep;
        var range = Math.Max(tickStep, scaleMax - scaleMin);
        var tickCount = (int)Math.Round(range / tickStep);
        var zeroPosition = (scaleMin + range) / range * plotHeight;
        for (var i = 0; i <= tickCount; i++)
        {
            var tickPosition = plotTop + plotHeight * i / tickCount;
            var rule = new Border { Height = 1, Background = ResourceBrush("ConsoleRuleBrush"), Opacity = .65, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, tickPosition, 0, 0) };
            Grid.SetColumnSpan(rule, periods.Count);
            RevenueChart.Children.Add(rule);
            RevenueAxis.Children.Add(new TextBlock
            {
                Text = FormatRevenueAxis(scaleMin + (tickCount - i) * tickStep),
                FontSize = 11,
                Foreground = ResourceBrush("ConsoleMutedInkBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, tickPosition - 8, 0, 0),
            });
        }
        var baseline = new Border { Height = 1, Background = ResourceBrush("ConsoleAccentBrush"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, plotTop + zeroPosition, 0, 0) };
        Grid.SetColumnSpan(baseline, periods.Count);
        RevenueChart.Children.Add(baseline);
        foreach (var row in periods)
        {
            RevenueChart.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var column = new Grid { Margin = new Thickness(4, 0, 4, 0) };
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plotTop) });
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plotHeight) });
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            var barHeight = (double)Math.Abs(row.Revenue) / range * plotHeight;
            var plot = new Grid();
            var bar = new Border
            {
                Height = barHeight,
                MaxWidth = 34,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, row.Revenue >= 0 ? Math.Max(0, zeroPosition - barHeight) : zeroPosition, 0, 0),
                Background = ResourceBrush(row.Revenue >= 0 ? "ConsoleFieldBrush" : "ConsoleMutedInkBrush"),
                BorderBrush = ResourceBrush("ConsoleAccentBrush"),
                BorderThickness = new Thickness(0, barHeight > 0 ? Math.Min(3, barHeight) : 0, 0, 0)
            };
            plot.Children.Add(bar);
            Grid.SetRow(plot, 1);
            var date = new TextBlock { Text = row.ChartLabel, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(date, 2);
            column.Children.Add(plot);
            column.Children.Add(date);
            AutomationProperties.SetName(column, $"{row.TableLabel} · Revenue {row.RevenueDisplay}");
            ToolTipService.SetToolTip(column, $"{row.TableLabel} · Revenue {row.RevenueDisplay}");
            Grid.SetColumn(column, RevenueChart.ColumnDefinitions.Count - 1);
            RevenueChart.Children.Add(column);
        }
        UpdateChartHint();
    }

    private static double RevenueTickStep(double target)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(target)));
        var factor = (target / magnitude) switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10,
        };
        return factor * magnitude;
    }

    private static string FormatRevenueAxis(double value) => Math.Abs(value) switch
    {
        >= 1_000_000_000_000 => $"{value / 1_000_000_000_000:0.#}T",
        >= 1_000_000_000 => $"{value / 1_000_000_000:0.#}B",
        >= 1_000_000 => $"{value / 1_000_000:0.#}M",
        >= 1_000 => $"{value / 1_000:0.#}K",
        _ => $"{value:0.##}",
    };

    private void OnChartSizeChanged(object sender, SizeChangedEventArgs args) => UpdateChartHint();
    private void UpdateChartHint()
    {
        if (RevenueChartHint is null) return;
        var columns = RevenueChart.ColumnDefinitions.Count;
        var viewportWidth = RevenueChartScroll.ActualWidth;
        if (columns > 0) RevenueChart.Width = Math.Max(RevenueChart.MinWidth, viewportWidth);
        var unit = SelectedRevenueGrouping.ToString().ToLowerInvariant();
        RevenueChartHint.Text = columns == 1 ? $"Scroll horizontally to see the full {unit}." : $"Scroll horizontally to see all {columns:N0} {unit}s.";
        RevenueChartHint.Visibility = columns > 0 && RevenueChart.MinWidth > viewportWidth + 1 ? Visibility.Visible : Visibility.Collapsed;
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

    private void OnShowAssistant(object sender, RoutedEventArgs args) => SetAssistantVisibility(true);
    private void OnHideAssistant(object sender, RoutedEventArgs args) => SetAssistantVisibility(false);

    private void OnAssistantPanelKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Escape) return;
        args.Handled = true;
        SetAssistantVisibility(false);
    }

    private void SetAssistantVisibility(bool open, bool restoreFocus = true)
    {
        if (assistant is null) return;
        AssistantPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        AssistantLauncherButton.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        if (open)
        {
            AssistantQuestionBox.Focus(FocusState.Programmatic);
            if (AssistantMessages.Children.Count > 0) ScrollToLatestAssistantMessage();
        }
        else if (restoreFocus) AssistantLauncherButton.Focus(FocusState.Programmatic);
    }

    private void OnReportsGettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        if (AssistantPanel?.Visibility == Visibility.Visible && args.InputDevice == FocusInputDeviceKind.Keyboard)
            SetAssistantVisibility(false, restoreFocus: false);
    }

    private void OnAssistantOverlaySizeChanged(object sender, SizeChangedEventArgs args)
    {
        // Size the overlay to the viewport. Its canvas contributes no desired size to the dashboard.
        var size = args.NewSize;
        AssistantOverlay.Width = size.Width;
        AssistantOverlay.Height = size.Height;
        AssistantLauncherButton.Width = Math.Min(276, size.Width);
        AssistantPanel.Width = Math.Min(420, size.Width);
        AssistantPanel.Height = Math.Min(560, size.Height);
    }

    private void OnAssistantDates(object sender, RoutedEventArgs args)
    {
        if (!model.IsBusy && sender is Button button) PeriodFlyout.ShowAt(button);
    }

    private void OnAssistantPrompt(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Content: TextBlock { Text: string question } } button) return;
        var focus = button.FocusState == FocusState.Keyboard ? FocusState.Keyboard : FocusState.Programmatic;
        AssistantQuestionBox.Text = question;
        AssistantQuestionBox.Focus(focus);
        AssistantQuestionBox.SelectionStart = question.Length;
    }

    private void OnAssistantQuestionChanged(object sender, TextChangedEventArgs args) => UpdateAssistantAction();

    private async void OnAssistantQuestionKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == VirtualKey.Enter && AssistantAskButton.IsEnabled)
        {
            args.Handled = true;
            await AskAssistantAsync();
        }
    }

    private async void OnAskAssistant(object sender, RoutedEventArgs args) => await AskAssistantAsync();

    private void UpdateAssistantAction()
    {
        if (AssistantAskButton is null || model is null) return;
        AssistantAskButton.IsEnabled = assistant is not null && !askingAssistant && !model.IsBusy &&
            model.FirstDate != default && !string.IsNullOrWhiteSpace(AssistantQuestionBox.Text);
    }

    private async Task AskAssistantAsync()
    {
        if (assistant is null || !AssistantAskButton.IsEnabled) return;
        var question = AssistantQuestionBox.Text.Trim();
        var request = new AssistantAskRequest(question, model.FirstDate, model.LastDate, TimeZoneInfo.Local.Id);
        askingAssistant = true;
        AssistantExamples.IsExpanded = false;
        AddAssistantMessage(question, fromUser: true);
        AssistantQuestionBox.Text = "";
        AssistantQuestionBox.Focus(FocusState.Programmatic);
        AssistantProgress.Visibility = Visibility.Visible;
        AssistantSpinner.IsActive = true;
        ScrollToLatestAssistantMessage();
        try
        {
            var response = await assistant.AskAsync(request);
            var sources = response.ToolsInvoked.Select(tool => AssistantSourceName(tool.Tool)).Distinct();
            var source = response.ToolsInvoked.Count > 0 ? $"Reports used · {string.Join(", ", sources)}" : null;
            var datedReports = response.ToolsInvoked.Where(tool => tool.Tool != AssistantTools.LowStock).ToArray();
            var followUp = datedReports.Length > 0 && datedReports.All(IsEmptyPeriodReport)
                ? "Use Change dates to try a wider or earlier period."
                : null;
            AddAssistantMessage(response.Answer, source: source, followUp: followUp);
        }
        catch (ApiException exception)
        {
            ShowAssistantError(question, exception.Kind switch
            {
                ApiErrorKind.Network => "Check your connection and try again.",
                ApiErrorKind.Unauthorized => "Your session has expired. Sign in again and retry.",
                ApiErrorKind.Forbidden => "Your account does not have access to the reporting assistant.",
                ApiErrorKind.Validation => "Check your question and report dates, then try again.",
                _ => "The reporting assistant is temporarily unavailable. Try again in a moment.",
            });
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            ShowAssistantError(question, "The reporting assistant couldn't finish the request. Try again in a moment.");
        }
        finally
        {
            AssistantProgress.Visibility = Visibility.Collapsed;
            AssistantSpinner.IsActive = false;
            askingAssistant = false;
            UpdateAssistantAction();
            ScrollToLatestAssistantMessage();
        }
    }

    private void ShowAssistantError(string question, string message)
    {
        AddAssistantMessage(message, isError: true);
        if (string.IsNullOrWhiteSpace(AssistantQuestionBox.Text)) AssistantQuestionBox.Text = question;
    }

    private void AddAssistantMessage(string message, bool fromUser = false, string? source = null,
        string? followUp = null, bool isError = false)
    {
        var ink = ResourceBrush(fromUser ? "ConsoleOnFieldBrush" : "ConsoleInkBrush");
        var bubble = new StackPanel { Spacing = 5 };
        var senderLabel = fromUser ? "You" : isError ? "Couldn't get an answer" : "Assistant";
        bubble.Children.Add(new TextBlock
        {
            Text = senderLabel,
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = ResourceBrush(fromUser ? "ConsoleOnFieldMutedBrush" : "ConsoleMutedInkBrush"),
        });
        var body = new TextBlock { FontSize = 14, Foreground = ink, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        if (!fromUser) AutomationProperties.SetLiveSetting(body, AutomationLiveSetting.Polite);
        bubble.Children.Add(body);
        if (followUp is not null) bubble.Children.Add(AssistantMessageDetail(followUp));
        if (source is not null) bubble.Children.Add(AssistantMessageDetail(source));

        AssistantMessages.Children.Add(new Border
        {
            Padding = new Thickness(12, 9, 12, 10),
            Margin = fromUser ? new Thickness(36, 0, 0, 0) : new Thickness(0, 0, 24, 0),
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = fromUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = ResourceBrush(fromUser ? "ConsoleFieldBrush" : "ConsoleRaisedGroundBrush"),
            Child = bubble,
        });
        body.Text = message;
    }

    private static TextBlock AssistantMessageDetail(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = ResourceBrush("ConsoleMutedInkBrush"),
        TextWrapping = TextWrapping.Wrap,
    };

    private void ScrollToLatestAssistantMessage()
    {
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (XamlRoot is null || AssistantPanel.Visibility != Visibility.Visible) return;
            AssistantTranscriptScroll.UpdateLayout();
            AssistantTranscriptScroll.ChangeView(null, AssistantTranscriptScroll.ScrollableHeight, null, true);
        });
    }

    private static string AssistantSourceName(string tool) => tool switch
    {
        AssistantTools.Revenue => "Revenue report",
        AssistantTools.BestSellers => "Best sellers report",
        AssistantTools.Channels => "Order channels report",
        AssistantTools.LowStock => "Current low-stock report",
        _ => "Reports",
    };

    private static bool IsEmptyPeriodReport(AssistantToolResult result)
    {
        if (result.Metrics is not JsonElement { ValueKind: JsonValueKind.Object } metrics) return false;
        return result.Tool switch
        {
            AssistantTools.BestSellers => metrics.TryGetProperty("items", out var items) &&
                items.ValueKind == JsonValueKind.Array && items.GetArrayLength() == 0,
            AssistantTools.Revenue or AssistantTools.Channels => metrics.TryGetProperty("orderCount", out var count) &&
                count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var orders) && orders == 0,
            _ => false,
        };
    }

    private void OnReportsHeaderSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (RightColumn is null) return;
        var reportWidth = args.NewSize.Width + 48;
        var wide = reportWidth >= 900;
        RightColumn.Width = wide ? new GridLength(.85, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(BestPanel, wide ? 1 : 0);
        Grid.SetRow(BestPanel, wide ? 0 : 1);
        Grid.SetRow(ChannelPanel, wide ? 1 : 2);
        Grid.SetColumn(StockPanel, wide ? 1 : 0);
        Grid.SetRow(StockPanel, wide ? 1 : 3);
        var fourKpis = reportWidth >= 800;
        Kpis.ColumnDefinitions[2].Width = Kpis.ColumnDefinitions[3].Width = fourKpis ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(AverageKpi, fourKpis ? 2 : 0);
        Grid.SetColumn(StockKpi, fourKpis ? 3 : 1);
        Grid.SetRow(AverageKpi, fourKpis ? 0 : 1);
        Grid.SetRow(StockKpi, fourKpis ? 0 : 1);
        Dashboard.ColumnSpacing = wide ? 24 : 0;
        var headerInline = reportWidth >= 600;
        Header.ColumnDefinitions[1].Width = headerInline ? GridLength.Auto : new GridLength(0);
        Header.ColumnSpacing = headerInline ? 18 : 0;
        Grid.SetColumn(HeaderActions, headerInline ? 1 : 0);
        Grid.SetColumnSpan(HeaderActions, headerInline ? 1 : 2);
        Grid.SetRow(HeaderActions, headerInline ? 0 : 1);
        ReportsTitle.FontSize = headerInline ? 42 : 34;
    }
}
