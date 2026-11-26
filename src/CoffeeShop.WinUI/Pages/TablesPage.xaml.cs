using System.ComponentModel;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class TablesPage : Page
{
    private readonly TableManagementWorkspace model;
    private bool rendering;

    public sealed record TableRow(TableManagementSummary Table)
    {
        public EntityId Id => Table.Id;
        public string Number => Table.TableNumber;
        public string Seats => Table.Seats is { } seats ? $"{seats} seats" : "Seats not set";
        public string Status => Table.IsOccupied ? "OCCUPIED" : "AVAILABLE";
        public string Association => Table.IsOccupied
            ? Table.ActiveOrderNumber ?? "Order details unavailable"
            : "Ready for a new party";
        public bool CanRemove => !Table.IsOccupied;
        public string RemoveHint => Table.IsOccupied
            ? "Release the table by completing its order first."
            : "Delete this table.";
        public string EditName => $"Edit table {Number}";
        public string RemoveName => $"Delete table {Number}";
        public Brush StatusBrush => Table.IsOccupied
            ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ConsoleAccentTextBrush"]
            : (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ConsoleMutedInkBrush"];
    }

    public TablesPage()
    {
        InitializeComponent();
        model = App.Services.GetRequiredService<TableManagementWorkspace>();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        model.PropertyChanged += OnChanged;
        await model.LoadAsync();
        ApplyLayout();
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => model.PropertyChanged -= OnChanged;

    private void OnChanged(object? sender, PropertyChangedEventArgs args) => Render();

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args) => ApplyLayout();

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is ListViewItem item && args.Item is TableRow row)
        {
            AutomationProperties.SetName(item,
                $"{row.Number}, {row.Seats}, {row.Status.ToLowerInvariant()}, {row.Association}");
        }
    }

    private void ApplyLayout()
    {
        if (Header is null || ActualWidth <= 0) return;
        var inline = ActualWidth >= 600;
        HeaderActionColumn.Width = inline ? GridLength.Auto : new GridLength(0);
        Grid.SetColumn(HeaderActions, inline ? 1 : 0);
        Grid.SetColumnSpan(HeaderActions, inline ? 1 : 2);
        Grid.SetRow(HeaderActions, inline ? 0 : 1);
        HeaderActions.HorizontalAlignment = inline ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    }

    internal void SetViewportWidth(double width)
    {
        if (!double.IsFinite(width) || width <= 0) return;
        RootGrid.Width = width;
        RootGrid.MaxWidth = width;
        ApplyLayout();
    }

    private void Render()
    {
        if (rendering) return;
        rendering = true;
        try
        {
            LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
            Status.Apply(model.Feedback);
            if (model.Feedback.Kind is WorkflowFeedbackKind.Ready or WorkflowFeedbackKind.Empty)
                Status.IsOpen = false;
            var occupied = model.Tables.Count(table => table.IsOccupied);
            SummaryText.Text = model.Tables.Count == 0
                ? "No dining tables yet."
                : $"{model.Tables.Count} tables · {occupied} occupied";
            TablesList.ItemsSource = model.Tables.Select(table => new TableRow(table)).ToArray();
            EmptyText.Visibility = !model.IsBusy && model.Tables.Count == 0 && model.Error is null
                ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            rendering = false;
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs args) => await model.LoadAsync();

    private async void OnNewTable(object sender, RoutedEventArgs args) => await EditTableAsync(null);

    private void OnEditTable(object sender, RoutedEventArgs args)
    {
        if (((FrameworkElement)sender).Tag is TableRow row) _ = EditTableAsync(row.Table);
    }

    private async void OnRemoveTable(object sender, RoutedEventArgs args)
    {
        if (((FrameworkElement)sender).Tag is not TableRow row || !row.CanRemove) return;
        var dialog = new ContentDialog
        {
            Title = new TextBlock
            {
                Text = $"Delete table {row.Number}?",
                FontFamily = (FontFamily)Resource("ConsoleSerifFont"),
                FontSize = 30,
                TextWrapping = TextWrapping.Wrap
            },
            Style = (Style)Resources["TableDialog"],
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            RequestedTheme = ElementTheme.Light,
            Content = new TextBlock
            {
                Text = "The table is deleted permanently. Past orders keep their totals but are no longer linked to this table. Tables with an active order cannot be deleted.",
                TextWrapping = TextWrapping.Wrap
            }
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await model.DeleteAsync(row.Id);
    }

    private async Task EditTableAsync(TableManagementSummary? table)
    {
        var number = Field("Table number", table?.TableNumber, 20, "T09");
        var seats = Field("Seats (optional)", table?.Seats?.ToString(), 3, "4");
        seats.InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } };
        var error = new InfoBar { IsOpen = false, IsClosable = false, Severity = InfoBarSeverity.Error };
        var progress = new ProgressBar { IsIndeterminate = true, Visibility = Visibility.Collapsed };
        var fields = new StackPanel { Spacing = 12 };
        fields.Children.Add(number);
        fields.Children.Add(seats);
        var editor = new ContentControl { Content = fields, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var dialog = new ContentDialog
        {
            Title = new TextBlock
            {
                Text = table is null ? "New table" : $"Edit table {table.TableNumber}",
                FontFamily = (FontFamily)Resource("ConsoleSerifFont"),
                FontSize = 32,
                TextWrapping = TextWrapping.Wrap
            },
            Style = (Style)Resources["TableDialog"],
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = ElementTheme.Light,
            FontFamily = (FontFamily)Resource("ConsoleSansFont"),
            FontSize = 14,
            Content = new ScrollViewer
            {
                Content = Form(error, progress, editor),
                MaxHeight = 420,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        };
        dialog.Closing += (_, args) => args.Cancel = model.IsBusy;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                error.IsOpen = false;
                progress.Visibility = Visibility.Visible;
                editor.IsEnabled = false;
                dialog.IsPrimaryButtonEnabled = false;
                if (!TryParseSeats(seats.Text, out var parsedSeats, out var seatsError))
                {
                    error.Message = seatsError;
                    error.IsOpen = true;
                    args.Cancel = true;
                    return;
                }

                var saved = await model.SaveAsync(table?.Id, number.Text, parsedSeats);
                args.Cancel = !saved;
                if (!saved)
                {
                    error.Message = model.Error?.Description ?? "The table could not be saved.";
                    error.IsOpen = true;
                }
            }
            finally
            {
                editor.IsEnabled = true;
                progress.Visibility = Visibility.Collapsed;
                dialog.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };
        await dialog.ShowAsync();
    }

    private static bool TryParseSeats(string text, out int? seats, out string error)
    {
        seats = null;
        error = "";
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text.Trim(), out var value) || value <= 0)
        {
            error = "Seats must be a whole number greater than zero, or left blank.";
            return false;
        }

        seats = value;
        return true;
    }

    private static TextBox Field(string header, string? value, int maxLength, string placeholder) => new()
    {
        Header = header,
        Text = value ?? "",
        MaxLength = maxLength,
        PlaceholderText = placeholder,
        FontSize = 14,
        MinHeight = 44,
        Style = (Style)Resource("ConsoleFieldStyle")
    };

    private static object Resource(string key) => Microsoft.UI.Xaml.Application.Current.Resources[key];

    private static StackPanel Form(params UIElement[] elements)
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var element in elements) panel.Children.Add(element);
        return panel;
    }
}
