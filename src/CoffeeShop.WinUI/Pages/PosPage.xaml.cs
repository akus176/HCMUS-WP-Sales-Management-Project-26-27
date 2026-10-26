using System.ComponentModel;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class PosPage : Page
{
    private readonly PosWorkspace model;
    private bool rendering;

    public PosPage()
    {
        InitializeComponent();
        model = App.Services.GetRequiredService<PosWorkspace>();
        RestoreForm();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        model.PropertyChanged += OnModelChanged;
        await model.LoadAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => model.PropertyChanged -= OnModelChanged;
    private void OnModelChanged(object? sender, PropertyChangedEventArgs args) => Render();
    private async void OnReload(object sender, RoutedEventArgs args) => await model.LoadAsync();
    private async void OnSearch(object sender, RoutedEventArgs args)
    {
        model.Search = SearchBox.Text;
        await model.LoadAsync();
    }

    private void OnOperatorChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering) model.Session.Operator = OperatorBox.SelectedItem as PosStaffOption;
    }

    private void OnChannelChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering || model is null) return;
        if (ChannelBox.SelectedItem is ComboBoxItem item) model.Channel = (string)item.Tag;
        UpdateChannel();
    }

    private void UpdateChannel()
    {
        TableBox.Visibility = model.Channel == "dine_in" ? Visibility.Visible : Visibility.Collapsed;
        DeliveryPanel.Visibility = model.Channel == "delivery" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAddressModeChanged(object sender, RoutedEventArgs args)
    {
        if (AddressBox is not null) AddressBox.IsEnabled = SavedAddressCheck.IsChecked != true;
    }

    private void OnChooseCustomer(object sender, RoutedEventArgs args)
    {
        CaptureForm();
        if (((App)Microsoft.UI.Xaml.Application.Current).Window is MainWindow main) main.NavigateTo("customers");
    }

    private void OnTableChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering && model is not null) model.TableId = (TableBox.SelectedItem as PosTableOption)?.Id;
    }

    private void OnUseCustomer(object sender, RoutedEventArgs args)
    {
        model.UseCustomerSelection();
        RestoreCustomer();
    }

    private void OnWalkIn(object sender, RoutedEventArgs args)
    {
        model.WalkIn();
        RestoreCustomer();
    }

    private void OnDrinkChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering) return;
        var item = DrinkBox.SelectedItem as MenuItemSummary;
        SizeBox.ItemsSource = item?.SizeVariants.Where(size => size.IsActive).ToArray();
        SizeBox.SelectedIndex = -1;
        AddonsList.ItemsSource = item?.CompatibleAddons.Where(addon => addon.IsActive).ToArray();
        AddonPricesText.Text = item is null ? "" : string.Join("\n", item.CompatibleAddons.Where(addon => addon.IsActive)
            .Select(addon => $"{addon.Name}: +{addon.Price:0.00} per drink"));
        SizePriceText.Text = "Choose a size to see its unit price.";
    }

    private void OnSizeChanged(object sender, SelectionChangedEventArgs args) =>
        SizePriceText.Text = SizeBox.SelectedItem is SizeVariantSummary size ? $"{size.SizeName}: {size.Price:0.00} per drink" : "Choose a size.";

    private void OnAddLine(object sender, RoutedEventArgs args)
    {
        if (DrinkBox.SelectedItem is not MenuItemSummary item || SizeBox.SelectedItem is not SizeVariantSummary size
            || !int.TryParse(QuantityBox.Text, out var quantity) || quantity <= 0)
        {
            model.ShowValidation("Choose a drink and size, and enter a positive whole-number quantity.");
            return;
        }

        model.AddLine(item, size, quantity, AddonsList.SelectedItems.Cast<AddonSummary>().ToArray());
    }

    private void OnRemoveLine(object sender, RoutedEventArgs args)
    {
        if (CartList.SelectedItem is PosCartLine line) model.RemoveLine(line);
    }

    private async void OnSaveDraft(object sender, RoutedEventArgs args)
    {
        CaptureForm();
        await model.SaveDraftAsync();
    }

    private void CaptureForm()
    {
        model.TableId = (TableBox.SelectedItem as PosTableOption)?.Id;
        model.Customer = model.Customer with
        {
            RecipientName = RecipientBox.Text,
            ContactNumber = ContactBox.Text,
            CustomerAddressId = SavedAddressCheck.IsChecked == true ? model.Customer.CustomerAddressId : null,
            AddressLine = AddressBox.Text
        };
    }

    private async void OnRequestDiscount(object sender, RoutedEventArgs args)
    {
        if (!CatalogNumberInput.TryParse(DiscountValueBox.Text, out var value))
        {
            model.ShowValidation("Enter a decimal discount value using your local decimal separator, without grouping separators.");
            return;
        }

        await model.RequestDiscountAsync((string)((ComboBoxItem)DiscountTypeBox.SelectedItem).Tag, value, DiscountReasonBox.Text);
    }

    private async void OnConfirm(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Confirm this ticket?",
            Content = $"Confirm {model.Order?.OrderNumber} for {model.Order?.Total:0.00}? This deducts stock and occupies the dine-in table.",
            PrimaryButtonText = "Confirm order",
            CloseButtonText = "Keep reviewing",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary) await model.ConfirmAsync();
    }

    private async void OnRedeemLoyalty(object sender, RoutedEventArgs args)
    {
        if (!int.TryParse(LoyaltyPointsBox.Text, out var points) || points <= 0)
        {
            model.ShowValidation("Enter a positive whole number of loyalty points.");
            return;
        }
        await model.RedeemLoyaltyAsync(points);
    }

    private async void OnNewTicket(object sender, RoutedEventArgs args)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Start a new ticket?",
            Content = "Unsaved selections will be cleared. Saved tickets remain in the database; copy the order ID to reopen one later.",
            PrimaryButtonText = "New ticket",
            CloseButtonText = "Keep ticket",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        model.NewTicket();
        RestoreForm();
    }

    private async void OnOpen(object sender, RoutedEventArgs args)
    {
        if (!Guid.TryParse(OpenIdBox.Text, out var id) || id == Guid.Empty)
        {
            model.ShowValidation("Enter the full order ID shown on a saved ticket.");
            return;
        }

        await model.OpenAsync(new EntityId(id));
    }

    private void RestoreForm()
    {
        rendering = true;
        ChannelBox.SelectedIndex = model.Channel switch { "dine_in" => 0, "delivery" => 2, _ => 1 };
        SearchBox.Text = model.Search;
        rendering = false;
        RestoreCustomer();
        UpdateChannel();
    }

    private void RestoreCustomer()
    {
        CustomerText.Text = $"Customer: {model.CustomerLabel}";
        RecipientBox.Text = model.Customer.RecipientName ?? "";
        ContactBox.Text = model.Customer.ContactNumber ?? "";
        AddressBox.Text = model.Customer.AddressLine ?? "";
        SavedAddressCheck.IsChecked = model.Customer.CustomerAddressId is not null;
        SavedAddressCheck.IsEnabled = model.Customer.CustomerAddressId is not null;
        SavedAddressText.Text = model.Customer.CustomerAddressId is null ? "No saved address attached. Enter an order-only address."
            : $"Saved address: {model.Customer.AddressLine ?? "Resolved again when saved"}";
    }

    private void Render()
    {
        rendering = true;
        LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        Status.Apply(model.Feedback);
        ReloadButton.IsEnabled = !model.IsBusy;
        EntryPanel.IsEnabled = model.CanEdit;
        OperatorBox.IsEnabled = !model.IsBusy;
        OperatorBox.ItemsSource = model.Options.Staff;
        OperatorBox.SelectedItem = model.Options.Staff.FirstOrDefault(staff => staff.Id == model.Session.Id);
        if (!ReferenceEquals(DrinkBox.ItemsSource, model.Menu))
        {
            DrinkBox.ItemsSource = model.Menu;
            DrinkBox.SelectedIndex = -1;
            SizeBox.ItemsSource = null;
            AddonsList.ItemsSource = null;
            SizePriceText.Text = "Choose a drink and size.";
            AddonPricesText.Text = "";
        }
        TableBox.ItemsSource = model.Options.Tables;
        TableBox.SelectedItem = model.Options.Tables.FirstOrDefault(table => table.Id == model.TableId);
        CartList.ItemsSource = model.Cart;
        EmptyText.Visibility = model.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        ConfirmButton.IsEnabled = model.CanConfirm;
        DiscountPanel.IsEnabled = model.CanConfirm;
        NewButton.IsEnabled = !model.IsBusy && !model.RequiresReload;
        OpenButton.IsEnabled = !model.IsBusy;
        TicketIdText.Text = $"Order ID: {model.TicketId}";
        TicketText.Text = model.Order is { } order ? FormatTicket(order) : "Save the draft to see persisted names, sizes, topping prices and exact totals here.";
        rendering = false;
    }

    private static string FormatTicket(PosOrderDetail order) =>
        $"{order.OrderNumber}\n{order.Channel} / {order.Status}\nCustomer: {order.CustomerName ?? "Walk-in"}"
        + (order.TableNumber is null ? "" : $"\nTable: {order.TableNumber}")
        + (order.AddressLine is null ? "" : $"\nDeliver to: {order.RecipientName}, {order.ContactNumber}\n{order.AddressLine}")
        + "\n\n" + string.Join("\n\n", order.Lines.Select(line =>
            $"{line.Quantity} x {line.ItemName} / {line.SizeName} @ {line.UnitPrice:0.00}"
            + string.Concat(line.Addons.Select(addon => $"\n+ {addon.Name} @ {addon.UnitPrice:0.00} per drink")) + $"\nLine total: {line.Total:0.00}"))
        + $"\n\nSubtotal: {order.Subtotal:0.00}\nApproved discounts: {order.DiscountTotal:0.00}\nPayable total: {order.Total:0.00}"
        + string.Concat(order.Discounts.Select(discount => $"\n{discount.Type} {discount.Value:0.00}: {discount.Status} (applied {discount.Amount ?? 0:0.00})"));
}
