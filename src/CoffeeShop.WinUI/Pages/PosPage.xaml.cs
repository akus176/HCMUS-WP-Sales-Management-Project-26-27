using System.ComponentModel;
using CoffeeShop.Application.Catalog;
using CoffeeShop.Application.Customers;
using CoffeeShop.Application.Sales;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CoffeeShop.WinUI.Pages;

public sealed record CounterCategory(EntityId? Id, string Label, bool Selected);

public sealed record CounterSavedLine(PosLineSnapshot Line)
{
    public string Name => Line.ItemName;
    public string Details => Line.Addons.Count == 0 ? Line.SizeName
        : $"{Line.SizeName} · {string.Join(" · ", Line.Addons.Select(addon => addon.Name))}";
    public string Quantity => $"{Line.Quantity} ×";
    public string Amount => Line.Total.ToString("0.00");
}

public sealed record CounterMenuCard(MenuItemSummary Item)
{
    private static readonly Dictionary<string, string> Photos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Americano"] = "espresso",
        ["Cappuccino"] = "cappuccino",
        ["Flat White"] = "flat-white",
        ["Espresso Tonic"] = "iced-coffee",
        ["Cold Brew"] = "cold-brew",
        ["Vietnamese Iced Coffee"] = "iced-coffee",
        ["Matcha Latte"] = "matcha",
        ["Chai Latte"] = "chai",
        ["Peach Iced Tea"] = "iced-tea",
        ["Butter Croissant"] = "croissant",
        ["Almond Croissant"] = "croissant",
        ["Banana Bread"] = "banana-bread",
        ["House Espresso"] = "espresso",
        ["Cafe Latte"] = "latte",
        ["Vanilla Latte"] = "latte",
        ["Caramel Macchiato"] = "latte",
        ["Mocha"] = "cappuccino",
        ["Coconut Cold Brew"] = "cold-brew",
        ["Peach Green Tea"] = "iced-tea",
        ["Jasmine Milk Tea"] = "chai",
        ["Strawberry Frappe"] = "iced-coffee",
        ["Chocolate Frappe"] = "iced-coffee",
        ["Mango Smoothie"] = "iced-tea",
        ["Blueberry Muffin"] = "bakery",
        ["Pumpkin Spice Latte"] = "latte",
        ["Honey Oat Latte"] = "latte"
    };
    public string Name =>
        Item.Name;

    public string Category =>
        Item.CategoryName.ToUpperInvariant();

    public string Price =>
        $"from {Item.SizeVariants.Where(size => size.IsActive).Min(size => size.Price):0.00}";

    public BitmapImage Photo
    {
        get
        {
            var name = Item.Name.StartsWith("Midterm ", StringComparison.OrdinalIgnoreCase)
                ? Item.Name[8..] : Item.Name;
            return new BitmapImage(new Uri($"ms-appx:///Assets/Photos/{(Photos.TryGetValue(name, out var file) ? file : "cafe-bar")}.jpg"));
        }
    }
}

public sealed partial class PosPage : Page
{
    internal object VerificationState() => new
    {
        model.TicketId,
        model.Search,
        model.CategoryId,
        model.Channel,
        model.TableId,
        model.Customer,
        model.CustomerLabel,
        cart = model.Cart.Select(line => line.Request),
        order = model.Order?.Id
    };

    internal async Task<object> PrepareVerificationAsync()
    {
        while (model.IsBusy) await Task.Delay(50);
        await model.LoadAsync();
        return new { items = model.Menu.Count, error = model.Error?.Code };
    }

    internal object VerificationLayout()
    {
        var panel = MenuGrid.ItemsPanelRoot as ItemsWrapGrid;
        var itemHeight = panel?.ItemHeight ?? 0;
        return new
        {
            menuHeight = MenuGrid.ActualHeight,
            itemHeight,
            fullyVisibleRows = itemHeight > 0 ? (int)Math.Floor((MenuGrid.ActualHeight + 1) / itemHeight) : 0,
            threeRowsFit = itemHeight > 0 && MenuGrid.ActualHeight + 1 >= itemHeight * 3,
            menuItems = MenuGrid.Items.Count,
            ticketWidth = TicketPanel.ActualWidth
        };
    }

    private readonly PosWorkspace model;
    private bool rendering;
    private EntityId? selectedMenuId;
    private bool customizationOpen;
    private CustomerOrderSelectionRequest? lastCustomerRequest;
    private CancellationTokenSource? customerSearchCancellation;
    private bool pageLoaded;
    private bool selectingCustomer;
    private EntityId? renderedCustomerId;
    private bool ticketCaptureActive;
    private PosStaffOption? captureOperator;
    private EntityId? selectedSavedAddressId;
    private string? selectedSavedAddressLine;
    private IReadOnlyList<MenuItemSummary>? renderedMenu;
    private IReadOnlyList<CategorySummary>? renderedCategories;
    private EntityId? renderedCategoryId;

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (PageLayout is null) return;
        // Use the workspace's available width, after the navigation rail.
        var wide = args.NewSize.Width >= 980 && args.NewSize.Height >= 640;
        PageLayout.Padding = new Thickness(args.NewSize.Width < 640 ? 16 : 24, 12, args.NewSize.Width < 640 ? 16 : 24, 16);
        PageScroll.VerticalScrollBarVisibility = wide ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        WorkspaceRow.Height = wide ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        MenuRow.Height = wide ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        TicketColumn.Width = wide ? new GridLength(390) : new GridLength(0);
        Grid.SetColumn(TicketPanel, wide ? 1 : 0);
        Grid.SetRow(TicketPanel, wide ? 0 : 1);
        TicketPanel.MinHeight = wide ? 0 : 680;
        TicketBodyScroll.MaxHeight = wide ? double.PositiveInfinity : 420;
        MenuItemsRow.Height = wide ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        MenuGrid.MaxHeight = wide ? double.PositiveInfinity : 540;
        HeaderActionColumn.Width = args.NewSize.Width < 640 ? new GridLength(0) : GridLength.Auto;
        Grid.SetColumn(NewButton, args.NewSize.Width < 640 ? 0 : 1);
        Grid.SetRow(NewButton, args.NewSize.Width < 640 ? 1 : 0);
    }

    private void OnMenuGridSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (MenuGrid.ItemsPanelRoot is not ItemsWrapGrid panel || args.NewSize.Width <= 0 || args.NewSize.Height <= 0) return;
        var available = Math.Max(1, args.NewSize.Width - 16); // Reserve the native scrollbar gutter.
        var columns = Math.Max(1, (int)(available / 184));
        panel.ItemWidth = Math.Floor(available / columns);
        // Use the actual list viewport so three complete rows fit, including cell gutters.
        panel.ItemHeight = Math.Clamp(Math.Floor(args.NewSize.Height / 3), 1, 208);
    }

    private async void OnClearSearch(object sender, RoutedEventArgs args)
    {
        ClearMenuSelection();
        SearchBox.Text = model.Search = "";
        model.SelectCategory(null);
        await model.LoadAsync();
        SearchBox.Focus(FocusState.Keyboard);
    }

    private void OnMenuItemClick(object sender, ItemClickEventArgs args)
    {
        if (args.ClickedItem is CounterMenuCard card) ShowCustomization(card);
    }

    private void OnMenuContainerChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is CounterMenuCard card)
            AutomationProperties.SetName(args.ItemContainer, $"{card.Name}, {card.Category}, {card.Price}. Choose size and add-ons.");
    }

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
        pageLoaded = true;
        model.PropertyChanged += OnModelChanged;
        await model.LoadAsync();
        if (!pageLoaded) return;
        var request = App.Services.GetRequiredService<CustomerOrderContext>().Request;
        var customerChanged = !ReferenceEquals(lastCustomerRequest, request);
        if (customerChanged && model.CanEdit)
        {
            model.UseCustomerSelection();
            RestoreCustomer();
        }
        lastCustomerRequest = request;
        await RefreshCustomerProfileAsync();
        if (customerChanged && pageLoaded)
            DispatcherQueue.TryEnqueue(() => { if (pageLoaded) TicketBodyScroll.ChangeView(null, 0, null, true); });
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        pageLoaded = false;
        customerSearchCancellation?.Cancel();
        CustomerSearchBox.IsSuggestionListOpen = false;
        if (model.CanEdit) CaptureForm();
        model.PropertyChanged -= OnModelChanged;
    }
    private void OnModelChanged(object? sender, PropertyChangedEventArgs args) => Render();
    private async void OnReload(object sender, RoutedEventArgs args)
    {
        ClearMenuSelection();
        await model.LoadAsync();
        await RefreshCustomerProfileAsync();
    }
    private async void OnSearch(object sender, RoutedEventArgs args)
    {
        ClearMenuSelection();
        model.Search = SearchBox.Text;
        await model.LoadAsync();
    }
    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter) OnSearch(sender, args);
    }
    private void OnCategoryClick(object sender, RoutedEventArgs args)
    {
        if (sender is not ToggleButton { Tag: CounterCategory category }) return;
        ClearMenuSelection();
        model.SelectCategory(category.Id);
    }
    private async void ShowCustomization(CounterMenuCard card)
    {
        if (!model.CanEdit || customizationOpen) return;
        customizationOpen = true;
        selectedMenuId = card.Item.Id;
        DrinkBox.SelectedItem = card.Item;
        QuantityBox.Text = "1";
        CustomizationPhoto.ImageSource = card.Photo;
        CustomizationError.IsOpen = false;
        CustomizationDialog.XamlRoot = XamlRoot;
        var container = MenuGrid.ContainerFromItem(MenuGrid.SelectedItem) as Control;
        try { await CustomizationDialog.ShowAsync(); }
        finally
        {
            customizationOpen = false;
            ClearMenuSelection(false);
            container?.Focus(FocusState.Keyboard);
        }
    }
    private void OnCustomizationAdd(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var previousCount = model.Cart.Count;
        OnAddLine(sender, new RoutedEventArgs());
        args.Cancel = model.Cart.Count == previousCount;
    }

    private void ClearMenuSelection(bool closeDialog = true)
    {
        selectedMenuId = null;
        if (MenuGrid is not null) MenuGrid.SelectedItem = null;
        if (DrinkBox is not null) DrinkBox.SelectedItem = null;
        if (closeDialog && customizationOpen) CustomizationDialog.Hide();
    }

    private void OnOperatorChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering) return;
        model.Session.Operator = OperatorBox.SelectedItem as PosStaffOption;
        Render();
    }

    private void OnChannelChecked(object sender, RoutedEventArgs args)
    {
        if (rendering || model is null || !model.CanEdit) return;
        if (sender is not RadioButton { IsChecked: true, Tag: string channel } || model.Channel == channel) return;
        CaptureForm();
        model.Channel = channel;
        Render();
    }

    private void UpdateChannel()
    {
        var channel = model.Order?.Channel ?? model.Channel;
        DineInTab.IsChecked = channel == "dine_in";
        TakeawayTab.IsChecked = channel == "takeaway";
        DeliveryTab.IsChecked = channel == "delivery";
        foreach (var tab in new[] { DineInTab, TakeawayTab, DeliveryTab })
        {
            var selected = (string)tab.Tag == channel;
            tab.Background = selected ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ConsolePanelBrush"] : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            tab.BorderBrush = selected ? (Brush)Microsoft.UI.Xaml.Application.Current.Resources["ConsoleAccentTextBrush"] : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            tab.Foreground = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[selected ? "ConsoleInkBrush" : "ConsoleMutedInkBrush"];
            tab.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
        ServiceFormHint.Text = model.Order is not null ? "Saved service details are read-only."
            : channel == "dine_in" ? "Choose a table for this dine-in order."
            : channel == "delivery" ? "Add the recipient and delivery address."
            : "Prepared for pickup at the counter.";
        TablePanel.Visibility = channel == "dine_in" && model.Order is null ? Visibility.Visible : Visibility.Collapsed;
        DeliveryPanel.Visibility = channel == "delivery" && model.Order is null ? Visibility.Visible : Visibility.Collapsed;
        SavedServiceText.Text = model.Order is not { } saved ? ""
            : saved.Channel == "delivery" ? $"Deliver to {saved.RecipientName} · {saved.ContactNumber}\n{saved.AddressLine}"
            : saved.TableNumber is not null ? $"Dine-in · Table {saved.TableNumber}" : "Takeaway service";
        SavedServiceText.Visibility = model.Order is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateTicketHeading();
    }

    private void OnToggleMoreOptions(object sender, RoutedEventArgs args) =>
        MoreOptionsPanel.Visibility = MoreOptionsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    private void UpdateTicketHeading()
    {
        TicketHeading.Text = model.Order is null ? "Your order" : "Order summary";
    }

    private void OnAddressModeChanged(object sender, RoutedEventArgs args)
    {
        if (AddressBox is null) return;
        var useSaved = SavedAddressCheck.IsChecked == true;
        AddressBox.Visibility = useSaved ? Visibility.Collapsed : Visibility.Visible;
        SavedAddressText.Visibility = useSaved ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SetCustomerSearchHint(string? message)
    {
        CustomerSearchHint.Text = message ?? "";
        CustomerSearchHint.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnCustomerSearchFocusChanged(object sender, RoutedEventArgs args)
    {
        if (XamlRoot is null) return;
        var insideSearch = false;
        for (var current = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current != CustomerSearchFrame) continue;
            insideSearch = true;
            break;
        }
        CustomerSearchFrame.BorderBrush = (Brush)Microsoft.UI.Xaml.Application.Current.Resources[
            insideSearch ? "ConsoleAccentTextBrush" : "ConsoleRuleBrush"];
    }

    private void OnCustomerSearchTapped(object sender, TappedRoutedEventArgs args)
    {
        if (model.CanEdit && !selectingCustomer) CustomerSearchBox.Focus(FocusState.Pointer);
    }

    private async void OnCustomerSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput || !pageLoaded || !model.CanEdit || selectingCustomer) return;
        customerSearchCancellation?.Cancel();
        var cancellation = customerSearchCancellation = new CancellationTokenSource();
        sender.ItemsSource = null;
        var query = sender.Text.Trim();
        if (query.Length == 0 && model.Customer.CustomerId is not null)
        {
            OnWalkIn(this, new RoutedEventArgs());
            return;
        }
        if (query.Length < 2)
        {
            SetCustomerSearchHint(query.Length == 0 ? null : "Type at least 2 characters to search.");
            sender.IsSuggestionListOpen = false;
            return;
        }
        SetCustomerSearchHint("Searching customers…");
        try
        {
            await Task.Delay(250, cancellation.Token);
            var result = await App.Services.GetRequiredService<ICustomerWorkspaceSource>().SearchAsync(new(query, false, 8));
            if (cancellation.IsCancellationRequested || !pageLoaded || !model.CanEdit) return;
            if (!result.IsSuccess)
            {
                SetCustomerSearchHint("Could not search customers. Try again or continue without a customer.");
                return;
            }
            var matches = result.Value.Where(customer => customer.IsActive).ToArray();
            sender.ItemsSource = matches;
            sender.IsSuggestionListOpen = matches.Length > 0;
            SetCustomerSearchHint(matches.Length == 0
                ? "No matches. Try another name or phone, or continue without a customer."
                : null);
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            if (!cancellation.IsCancellationRequested && pageLoaded)
                SetCustomerSearchHint("Could not search customers. Check the connection and try again.");
        }
    }

    private void OnCustomerSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is CustomerSummary customer) sender.Text = customer.FullName;
    }

    private async void OnCustomerQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is not CustomerSummary customer)
        {
            SetCustomerSearchHint("Choose a matching profile from the suggestions.");
            sender.IsSuggestionListOpen = sender.ItemsSource is IEnumerable<CustomerSummary> matches && matches.Any();
            return;
        }
        if (!model.CanEdit || selectingCustomer) return;
        customerSearchCancellation?.Cancel();
        var ticketId = model.TicketId;
        selectingCustomer = true;
        SetCustomerSearchHint("Loading customer…");
        Render();
        try
        {
            var result = await App.Services.GetRequiredService<ICustomerWorkspaceSource>().GetDetailAsync(customer.Id);
            if (!pageLoaded || ticketId != model.TicketId || !model.CanEdit) return;
            if (!result.IsSuccess || !result.Value.IsActive)
            {
                SetCustomerSearchHint("This profile is unavailable. Choose another customer.");
                return;
            }
            CaptureForm();
            model.SelectCustomer(result.Value);
            RestoreCustomer();
            SetCustomerSearchHint(null);
            sender.Text = result.Value.FullName;
            sender.ItemsSource = null;
            CustomerSearchBox.IsSuggestionListOpen = false;
        }
        catch (Exception)
        {
            if (pageLoaded) SetCustomerSearchHint("Could not load this customer. Check the connection and try again.");
        }
        finally
        {
            selectingCustomer = false;
            if (pageLoaded) Render();
        }
    }

    private async Task RefreshCustomerProfileAsync()
    {
        var id = model.Order is null ? model.Customer.CustomerId : model.Order.CustomerId;
        if (id is null) return;
        var ticketId = model.TicketId;
        try
        {
            var result = await App.Services.GetRequiredService<ICustomerWorkspaceSource>().GetDetailAsync(id);
            if (pageLoaded && ticketId == model.TicketId && result.IsSuccess) model.RefreshCustomerProfile(result.Value);
        }
        catch (Exception) { /* The saved ticket remains usable if balance lookup is unavailable. */ }
    }

    private void OnTableChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering && model is not null)
        {
            model.TableId = (TableBox.SelectedItem as PosTableOption)?.Id;
            UpdateTicketHeading();
            Render();
        }
    }

    private void OnUseCustomer(object sender, RoutedEventArgs args)
    {
        model.UseCustomerSelection();
        RestoreCustomer();
    }

    private void OnWalkIn(object sender, RoutedEventArgs args)
    {
        customerSearchCancellation?.Cancel();
        model.WalkIn();
        lastCustomerRequest = App.Services.GetRequiredService<CustomerOrderContext>().Request;
        CustomerSearchBox.Text = "";
        CustomerSearchBox.ItemsSource = null;
        SetCustomerSearchHint(null);
        RestoreCustomer();
        CustomerSearchBox.Focus(FocusState.Keyboard);
    }

    private void OnDrinkChanged(object sender, SelectionChangedEventArgs args)
    {
        if (rendering) return;
        var item = DrinkBox.SelectedItem as MenuItemSummary;
        SelectedDrinkName.Text = item?.Name ?? "";
        var sizes = item?.SizeVariants.Where(size => size.IsActive).OrderBy(size => size.Price).ToArray() ?? [];
        SizeBox.ItemsSource = sizes;
        SizeBox.SelectedIndex = sizes.Length > 0 ? 0 : -1;
        var addons = item?.CompatibleAddons.Where(addon => addon.IsActive).ToArray() ?? [];
        AddonsList.ItemsSource = addons;
        AddonsPanel.Visibility = addons.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSizeChanged(object sender, SelectionChangedEventArgs args) =>
        SizePriceText.Text = SizeBox.SelectedItem is SizeVariantSummary size ? $"{size.SizeName}: {size.Price:0.00} per drink" : "Choose a size.";

    private void OnAddLine(object sender, RoutedEventArgs args)
    {
        if (DrinkBox.SelectedItem is not MenuItemSummary item || SizeBox.SelectedItem is not SizeVariantSummary size
            || !int.TryParse(QuantityBox.Text, out var quantity) || quantity <= 0)
        {
            model.ShowValidation("Choose a drink and size, and enter a positive whole-number quantity.");
            CustomizationError.Message = "Choose a size and enter a positive whole-number quantity.";
            CustomizationError.IsOpen = true;
            return;
        }

        var previousCount = model.Cart.Count;
        model.AddLine(item, size, quantity, AddonsList.SelectedItems.Cast<AddonSummary>().ToArray());
        if (model.Cart.Count > previousCount) ClearMenuSelection(false);
        else
        {
            CustomizationError.Message = model.Message;
            CustomizationError.IsOpen = true;
        }
    }

    private void OnRemoveLine(object sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: PosCartLine line }) return;
        var keyboard = (sender as Control)?.FocusState == FocusState.Keyboard;
        var index = model.Cart.ToList().FindIndex(row => row.LineId == line.LineId);
        model.RemoveLine(line);
        RestoreTicketFocus(index, "Increase", keyboard);
    }
    private void OnIncreaseLine(object sender, RoutedEventArgs args)
    {
        ChangeLineQuantity(sender, 1);
    }
    private void OnDecreaseLine(object sender, RoutedEventArgs args)
    {
        ChangeLineQuantity(sender, -1);
    }

    private void ChangeLineQuantity(object sender, int delta)
    {
        if (sender is not Button { Tag: PosCartLine line } button) return;
        var keyboard = button.FocusState == FocusState.Keyboard;
        var index = model.Cart.ToList().FindIndex(row => row.LineId == line.LineId);
        if (index < 0) return;
        var current = model.Cart[index];
        if (delta > 0 && current.Quantity == int.MaxValue) return;
        model.ChangeQuantity(current, current.Quantity + delta);
        RestoreTicketFocus(index, delta > 0 ? "Increase" : "Decrease", keyboard);
    }

    private void RestoreTicketFocus(int index, string action, bool keyboard)
    {
        if (!keyboard) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (model.Cart.Count == 0) { MenuGrid.Focus(FocusState.Keyboard); return; }
            var line = model.Cart[Math.Clamp(index, 0, model.Cart.Count - 1)];
            CartList.UpdateLayout();
            FocusTicketButton(CartList, line.LineId, action);
        });
    }

    private static bool FocusTicketButton(DependencyObject parent, Guid lineId, string action)
        => FindTicketButton(parent, lineId, action)?.Focus(FocusState.Keyboard) == true;

    private static Button? FindTicketButton(DependencyObject parent, Guid lineId, string action)
    {
        if (parent is Button { Tag: PosCartLine line } button && line.LineId == lineId
            && AutomationProperties.GetName(button).StartsWith(action, StringComparison.Ordinal))
            return button;
        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
            if (FindTicketButton(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, index), lineId, action) is { } match) return match;
        return null;
    }

    private async void OnSaveDraft(object sender, RoutedEventArgs args)
    {
        if (!model.CanEdit || selectingCustomer || model.Cart.Count == 0) return;
        if (model.Session.Operator is null)
        {
            OperatorBox.Focus(FocusState.Keyboard);
            OperatorBox.StartBringIntoView();
            model.ShowValidation("Choose an operator before saving this ticket.");
            return;
        }
        CaptureForm();
        if (model.Channel == "dine_in" && model.TableId is null)
        {
            model.ShowValidation("Choose an available table for dine-in service.");
            TableBox.Focus(FocusState.Keyboard);
            TableBox.StartBringIntoView();
            return;
        }
        if (model.Channel == "delivery" && (string.IsNullOrWhiteSpace(RecipientBox.Text)
            || string.IsNullOrWhiteSpace(ContactBox.Text)
            || model.Customer.CustomerAddressId is null && string.IsNullOrWhiteSpace(AddressBox.Text)))
        {
            model.ShowValidation("Add a delivery recipient, contact number, and address before saving.");
            RecipientBox.Focus(FocusState.Keyboard);
            RecipientBox.StartBringIntoView();
            return;
        }
        await model.SaveDraftAsync();
    }

    private void CaptureForm()
    {
        model.TableId = (TableBox.SelectedItem as PosTableOption)?.Id;
        model.Customer = model.Customer with
        {
            RecipientName = RecipientBox.Text,
            ContactNumber = ContactBox.Text,
            CustomerAddressId = SavedAddressCheck.IsChecked == true ? selectedSavedAddressId : null,
            AddressLine = SavedAddressCheck.IsChecked == true ? selectedSavedAddressLine : AddressBox.Text
        };
    }

    private async void OnRequestDiscount(object sender, RoutedEventArgs args)
    {
        if (!model.CanConfirm) return;
        DiscountFeedback.IsOpen = false;
        if (!CatalogNumberInput.TryParse(DiscountValueBox.Text, out var value))
        {
            DiscountFeedback.Message = "Enter a decimal value using your local decimal separator, without grouping separators.";
            DiscountFeedback.IsOpen = true;
            DiscountValueBox.Focus(FocusState.Keyboard);
            return;
        }

        await model.RequestDiscountAsync((string)((ComboBoxItem)DiscountTypeBox.SelectedItem).Tag, value, DiscountReasonBox.Text);
        DiscountFeedback.Message = model.Error?.Description ?? "";
        DiscountFeedback.IsOpen = model.Error is not null;
        if (model.Error is null)
        {
            DiscountFlyout.Hide();
            DiscountValueBox.Text = "";
            DiscountReasonBox.Text = "";
        }
    }

    private async void OnConfirm(object sender, RoutedEventArgs args)
    {
        if (!model.CanConfirm) return;
        if (model.Session.Operator is null)
        {
            model.ShowValidation("Choose an operator before confirming this order.");
            OperatorBox.Focus(FocusState.Keyboard);
            OperatorBox.StartBringIntoView();
            return;
        }
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Confirm this ticket?",
            Content = $"Confirm {model.Order?.OrderNumber} for {model.Order?.Total:0.00}? This deducts stock and occupies the dine-in table.",
            PrimaryButtonText = "Confirm order",
            CloseButtonText = "Keep reviewing",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        await model.ConfirmAsync();
        if (model.Order is { Status: CoffeeShop.Domain.Sales.OrderStatus.Confirmed } confirmed)
            OpenServiceBoard(confirmed.Id);
    }

    private async void OnRedeemLoyalty(object sender, RoutedEventArgs args)
    {
        if (!model.CanConfirm || model.Order?.CustomerId is null) return;
        LoyaltyFeedback.IsOpen = false;
        if (!int.TryParse(LoyaltyPointsBox.Text, out var points) || points <= 0)
        {
            LoyaltyFeedback.Message = "Enter a positive whole number of points.";
            LoyaltyFeedback.IsOpen = true;
            LoyaltyPointsBox.Focus(FocusState.Keyboard);
            return;
        }
        await model.RedeemLoyaltyAsync(points);
        LoyaltyFeedback.Message = model.Error?.Description ?? "";
        LoyaltyFeedback.IsOpen = model.Error is not null;
        if (model.Error is null)
        {
            LoyaltyFlyout.Hide();
            LoyaltyPointsBox.Text = "";
            await RefreshCustomerProfileAsync();
        }
    }

    private async void OnNewTicket(object sender, RoutedEventArgs args)
    {
        if (selectingCustomer) return;
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
        customerSearchCancellation?.Cancel();
        lastCustomerRequest = App.Services.GetRequiredService<CustomerOrderContext>().Request;
        CustomerSearchBox.Text = "";
        CustomerSearchBox.ItemsSource = null;
        LoyaltyPointsBox.Text = DiscountValueBox.Text = DiscountReasonBox.Text = "";
        LoyaltyFeedback.IsOpen = DiscountFeedback.IsOpen = false;
        LoyaltyFlyout.Hide();
        DiscountFlyout.Hide();
        ClearMenuSelection();
        MoreOptionsPanel.Visibility = Visibility.Collapsed;
        RestoreForm();
        TicketBodyScroll.ChangeView(null, 0, null, true);
    }
    private void OnServiceBoard(object sender, RoutedEventArgs args)
    {
        if (model.Order is { } order) OpenServiceBoard(order.Id);
    }
    private static void OpenServiceBoard(EntityId orderId)
    {
        App.Services.GetRequiredService<OperationsWorkspace>().FocusOrder(orderId);
        if (((App)Microsoft.UI.Xaml.Application.Current).Window is MainWindow main) main.NavigateTo("service-board");
    }

    private async void OnOpen(object sender, RoutedEventArgs args)
    {
        if (selectingCustomer) return;
        if (!Guid.TryParse(OpenIdBox.Text, out var id) || id == Guid.Empty)
        {
            model.ShowValidation("Enter the full order ID shown on a saved ticket.");
            return;
        }

        await model.OpenAsync(new EntityId(id));
        await RefreshCustomerProfileAsync();
    }

    private void RestoreForm()
    {
        rendering = true;
        SearchBox.Text = model.Search;
        rendering = false;
        RestoreCustomer();
        UpdateChannel();
    }

    private void RestoreCustomer()
    {
        SetCustomerSearchHint(null);
        selectedSavedAddressId = model.Customer.CustomerAddressId;
        selectedSavedAddressLine = model.Customer.AddressLine;
        SetCustomerSummary(model.CustomerLabel, model.Customer.CustomerId is not null);
        RecipientBox.Text = model.Customer.RecipientName ?? "";
        ContactBox.Text = model.Customer.ContactNumber ?? "";
        AddressBox.Text = selectedSavedAddressId is null ? model.Customer.AddressLine ?? "" : "";
        SavedAddressCheck.IsChecked = model.Customer.CustomerAddressId is not null;
        SavedAddressCheck.IsEnabled = model.Customer.CustomerAddressId is not null;
        SavedAddressCheck.Visibility = selectedSavedAddressId is null ? Visibility.Collapsed : Visibility.Visible;
        OnAddressModeChanged(this, new RoutedEventArgs());
        SavedAddressText.Text = model.Customer.CustomerAddressId is null ? "No saved address attached. Enter an order-only address."
            : $"Saved address: {model.Customer.AddressLine ?? "Resolved again when saved"}";
    }

    private void SetCustomerSummary(string name, bool selected)
    {
        CustomerText.Text = selected ? name : "";
        CustomerText.Visibility = selected && model.Order is not null ? Visibility.Visible : Visibility.Collapsed;
        CustomerOptionsPanel.Visibility = model.Order is null ? Visibility.Visible : Visibility.Collapsed;
        CustomerMetaText.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        CustomerRow.Visibility = selected || model.Order is null ? Visibility.Visible : Visibility.Collapsed;
        var id = model.Order is null ? model.Customer.CustomerId : model.Order.CustomerId;
        if (renderedCustomerId != id)
        {
            CustomerSearchBox.Text = selected ? name : "";
            renderedCustomerId = id;
        }
        CustomerMetaText.Text = selected
            ? model.CustomerProfile is { } profile && profile.Id == id
                ? $"{profile.Phone} · {profile.LoyaltyPointsBalance} points"
                : "Profile attached · balance unavailable"
            : "";
    }

    private void Render()
    {
        rendering = true;
        LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        Status.Apply(model.Feedback);
        Status.IsOpen = model.Feedback.IsError && model.Menu.Count == 0 || model.Feedback.Kind == WorkflowFeedbackKind.Empty;
        TicketFeedback.IsOpen = model.Error is not null && model.Menu.Count > 0;
        TicketFeedback.Message = model.Error?.Description ?? "";
        RefreshTicketButton.Visibility = model.RequiresReload ? Visibility.Visible : Visibility.Collapsed;
        RefreshTicketButton.IsEnabled = !model.IsBusy;
        ReloadButton.IsEnabled = !model.IsBusy;
        SearchButton.IsEnabled = !model.IsBusy;
        ClearSearchButton.IsEnabled = !model.IsBusy;
        CategoryList.IsEnabled = !model.IsBusy;
        EntryPanel.IsEnabled = model.CanEdit;
        DineInTab.IsEnabled = model.CanEdit;
        TakeawayTab.IsEnabled = model.CanEdit;
        DeliveryTab.IsEnabled = model.CanEdit;
        CustomerOptionsPanel.IsEnabled = model.CanEdit && !selectingCustomer;
        TableBox.IsEnabled = model.CanEdit;
        MenuGrid.IsEnabled = model.CanEdit;
        OperatorBox.IsEnabled = !model.IsBusy;
        if (!ReferenceEquals(OperatorBox.ItemsSource, model.Options.Staff)) OperatorBox.ItemsSource = model.Options.Staff;
        OperatorBox.SelectedItem = model.Options.Staff.FirstOrDefault(staff => staff.Id == model.Session.Id);
        var menuChanged = !ReferenceEquals(renderedMenu, model.Menu) || renderedCategoryId != model.CategoryId;
        if (menuChanged || !ReferenceEquals(renderedCategories, model.Categories))
        {
            CategoryList.ItemsSource = new[] { new CounterCategory(null, $"ALL  {model.Menu.Count}", model.CategoryId is null) }
            .Concat(model.Categories.Select(category => new CounterCategory(category.Id,
                $"{category.Name.ToUpperInvariant()}  {model.Menu.Count(item => item.CategoryId == category.Id)}",
                model.CategoryId == category.Id))).ToArray();
            renderedCategories = model.Categories;
        }
        MenuHeading.Text = model.CategoryId is { } categoryId
            ? model.Categories.FirstOrDefault(category => category.Id == categoryId)?.Name ?? "All items"
            : "All items";
        MenuCount.Text = $"{model.VisibleMenu.Count} AVAILABLE";
        var visible = model.VisibleMenu;
        if (selectedMenuId is { } selectedId && visible.All(item => item.Id != selectedId))
        {
            selectedMenuId = null;
            if (customizationOpen) CustomizationDialog.Hide();
        }
        if (menuChanged)
        {
            var cards = visible.Select(item => new CounterMenuCard(item)).ToArray();
            MenuGrid.ItemsSource = cards;
            MenuGrid.SelectedItem = cards.FirstOrDefault(card => card.Item.Id == selectedMenuId);
            renderedMenu = model.Menu;
            renderedCategoryId = model.CategoryId;
        }
        if (!ReferenceEquals(DrinkBox.ItemsSource, model.Menu))
        {
            DrinkBox.ItemsSource = model.Menu;
            DrinkBox.SelectedItem = model.Menu.FirstOrDefault(item => item.Id == selectedMenuId);
            SizeBox.ItemsSource = null;
            AddonsList.ItemsSource = null;
            SizePriceText.Text = "Choose a drink and size.";
            AddonsPanel.Visibility = Visibility.Collapsed;
        }
        if (!ReferenceEquals(TableBox.ItemsSource, model.Options.Tables)) TableBox.ItemsSource = model.Options.Tables;
        TableBox.SelectedItem = model.Options.Tables.FirstOrDefault(table => table.Id == model.TableId);
        if (!ReferenceEquals(CartList.ItemsSource, model.Cart)) CartList.ItemsSource = model.Cart;
        CartList.IsEnabled = model.CanEdit;
        CartList.Visibility = model.Order is null && model.Cart.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ServiceSection.Visibility = model.Cart.Count > 0 || model.Order is not null ? Visibility.Visible : Visibility.Collapsed;
        SavedLinesList.ItemsSource = model.Order?.Lines.Select(line => new CounterSavedLine(line)).ToArray();
        SavedLinesList.Visibility = model.Order is { Lines.Count: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        TicketEmptyText.Visibility = model.Order is null && model.Cart.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var itemCount = model.Order is { } saved ? saved.Lines.Sum(line => line.Quantity) : model.Cart.Sum(line => line.Quantity);
        CartCountText.Text = $"{itemCount} {(itemCount == 1 ? "ITEM" : "ITEMS")}";
        EmptyPanel.Visibility = !model.IsBusy && model.Error is null && visible.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MenuGrid.Visibility = visible.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmButton.IsEnabled = model.CanConfirm;
        ConfirmButton.Visibility = model.Order is { Status: CoffeeShop.Domain.Sales.OrderStatus.Draft } ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Visibility = model.Order is null ? Visibility.Visible : Visibility.Collapsed;
        ServiceBoardButton.Visibility = model.Order is { Status: CoffeeShop.Domain.Sales.OrderStatus.Confirmed or CoffeeShop.Domain.Sales.OrderStatus.Preparing or CoffeeShop.Domain.Sales.OrderStatus.Ready }
            ? Visibility.Visible : Visibility.Collapsed;
        DiscountPanel.IsEnabled = model.CanConfirm;
        DiscountPanel.Visibility = model.Order?.Status == CoffeeShop.Domain.Sales.OrderStatus.Draft ? Visibility.Visible : Visibility.Collapsed;
        var loyaltySavings = model.Order is { } priced ? Math.Max(0, priced.Subtotal - priced.DiscountTotal - priced.Total) : 0;
        LoyaltyPanel.IsEnabled = model.CanConfirm && model.Order?.CustomerId is not null && loyaltySavings == 0;
        LoyaltyPanel.Visibility = model.Order is { Status: CoffeeShop.Domain.Sales.OrderStatus.Draft, CustomerId: not null } && loyaltySavings == 0
            ? Visibility.Visible : Visibility.Collapsed;
        LoyaltyButton.IsEnabled = DiscountButton.IsEnabled = !model.IsBusy && !selectingCustomer;
        var customerId = model.Order is null ? model.Customer.CustomerId : model.Order.CustomerId;
        var balance = model.CustomerProfile is { } profile && profile.Id == customerId
            ? $"Available balance: {profile.LoyaltyPointsBalance} points." : "The available balance will be checked when you apply points.";
        LoyaltyHintText.Text = customerId is null ? "Choose a customer before saving the draft to redeem their points."
            : model.Order is null ? $"{balance} Save the draft to redeem points."
            : loyaltySavings > 0 ? "Points have already been applied to this ticket. Review the loyalty savings in the total."
            : !model.CanConfirm ? "Points can be redeemed here while the ticket is a saved draft."
            : balance;
        DiscountHintText.Text = model.Order is null ? "Save the draft first, then request a discount before confirming."
            : !model.CanConfirm ? "Discounts can be requested here while the ticket is a saved draft."
            : "Enter the value and reason for manager approval.";
        AdjustmentsHintText.Text = model.Order is null ? "Save the draft to apply points or request a discount."
            : model.Order.Discounts.Any(discount => discount.Status == "pending") ? "Discount awaiting approval. Refresh after manager approval."
            : model.CanConfirm ? "Apply points or request a discount before confirming."
            : "Saved ticket details are read-only.";
        NewButton.IsEnabled = !model.IsBusy && !model.RequiresReload && !selectingCustomer;
        OpenButton.IsEnabled = !model.IsBusy && !selectingCustomer;
        SaveButton.IsEnabled = model.CanEdit && !selectingCustomer && model.Cart.Count > 0;
        TicketKickerText.Text = model.Order?.OrderNumber ?? "CURRENT TICKET";
        TicketStatusText.Text = model.Order?.Status.ToUpperInvariant() ?? "NEW";
        TicketIdText.Text = $"Order ID: {model.TicketId}";
        if (model.Order is { } opened)
        {
            SetCustomerSummary(opened.CustomerName ?? "Walk-in", opened.CustomerId is not null);
        }
        else SetCustomerSummary(model.CustomerLabel, model.Customer.CustomerId is not null);
        UpdateChannel();
        var estimatedSubtotal = model.Cart.Sum(line => (line.Size.Price + line.Addons.Sum(addon => addon.Price)) * line.Quantity);
        SubtotalText.Text = (model.Order?.Subtotal ?? estimatedSubtotal).ToString("0.00");
        TotalsBreakdown.Visibility = itemCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        DiscountTotalText.Text = model.Order is { DiscountTotal: > 0 } discounted ? $"−{discounted.DiscountTotal:0.00}" : "0.00";
        DiscountTotalRow.Visibility = model.Order is { DiscountTotal: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        LoyaltySavingsText.Text = loyaltySavings > 0 ? $"−{loyaltySavings:0.00}" : "0.00";
        LoyaltySavingsRow.Visibility = loyaltySavings > 0 ? Visibility.Visible : Visibility.Collapsed;
        TicketTotalLabel.Text = model.Order is null ? "Estimated total" : "Payable total";
        TotalHintText.Text = model.Order is null ? "Save to review the final total" : "Saved order total";
        TicketStepText.Text = model.RequiresReload ? "Refresh this ticket before continuing."
            : model.IsBusy ? "Updating your ticket…"
            : model.Order is { Status: CoffeeShop.Domain.Sales.OrderStatus.Draft } ? "Review the saved total, then confirm."
            : model.Order is { Status: CoffeeShop.Domain.Sales.OrderStatus.Confirmed or CoffeeShop.Domain.Sales.OrderStatus.Preparing or CoffeeShop.Domain.Sales.OrderStatus.Ready } ? "Open Service board to continue."
            : model.Order is not null ? $"Ticket {model.Order.Status}. Saved details are read-only."
            : model.Cart.Count == 0 ? "Choose a menu item to start."
            : model.Session.Operator is null ? "Choose an operator, then save."
            : model.Channel == "dine_in" && model.TableId is null ? "Choose an available table before saving."
            : model.Channel == "delivery" ? "Check the delivery details, then save to review."
            : "Save, review the total, then confirm.";
        TicketTotalText.Text = model.Order is { } current
            ? current.Total.ToString("0.00")
            : estimatedSubtotal.ToString("0.00");
        TicketText.Text = model.Order is { } order ? FormatTicket(order)
            : model.Cart.Count == 0 ? "Add a drink to begin. Save the draft to review exact totals."
            : "Save the draft for exact totals and discounts.";
        rendering = false;
    }

    private static string FormatTicket(PosOrderDetail order) =>
        $"{order.Channel} / {order.Status}\nCustomer: {order.CustomerName ?? "Walk-in"}"
        + (order.TableNumber is null ? "" : $"\nTable: {order.TableNumber}")
        + (order.AddressLine is null ? "" : $"\nDeliver to: {order.RecipientName}, {order.ContactNumber}\n{order.AddressLine}")
        + $"\nSubtotal: {order.Subtotal:0.00}\nApproved discounts: {order.DiscountTotal:0.00}"
        + string.Concat(order.Discounts.Select(discount => $"\n{discount.Type} {discount.Value:0.00}: {discount.Status} (applied {discount.Amount ?? 0:0.00})"));

    // Opt-in native verification. Uses local cart state and read-only catalog queries;
    // never saves, confirms, redeems points, or requests discounts.
    internal async Task<object> VerifyInteractionsAsync()
    {
        for (var attempt = 0; model.IsBusy && attempt < 50; attempt++) await Task.Delay(100);
        if (model.IsBusy || model.Menu.Count == 0 || model.Cart.Count > 0 || model.Order is not null)
            return new { skipped = true, reason = "A fresh, loaded menu is required." };
        var serviceHiddenUntilItems = ServiceSection.Visibility == Visibility.Collapsed;

        var item = model.Menu[0];
        var card = ((IEnumerable<CounterMenuCard>)MenuGrid.ItemsSource).First();
        MenuGrid.SelectedItem = card;
        ShowCustomization(card);
        await Task.Delay(150);
        var customization = customizationOpen && SizeBox.SelectedItem is SizeVariantSummary;
        if (customization && CustomizationDialog.Content is FrameworkElement { ActualWidth: > 0, ActualHeight: > 0 } popupContent)
        {
            // Capture the native frame as well as the content, so title and button
            // surface regressions are visible in the reference image.
            FrameworkElement surface = popupContent;
            for (DependencyObject? current = popupContent; current is not null;
                current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
                if (current is FrameworkElement { Name: "BackgroundElement" } frame) { surface = frame; break; }
            await PresentationVerification.CaptureAsync(surface, "native-counter-customization.png");
        }
        QuantityBox.Text = "0";
        OnAddLine(this, new RoutedEventArgs());
        var invalidStaysOpen = customizationOpen && CustomizationError.IsOpen && model.Cart.Count == 0;
        QuantityBox.Text = "1";
        CustomizationError.IsOpen = false;
        OnAddLine(this, new RoutedEventArgs());
        CustomizationDialog.Hide();
        await Task.Delay(150);
        var added = model.Cart.Count == 1 && SaveButton.IsEnabled && !ConfirmButton.IsEnabled;
        CartList.UpdateLayout();
        var orderFirst = ServiceSection.Visibility == Visibility.Visible
            && CartList.TransformToVisual(TicketBodyScroll).TransformPoint(new Windows.Foundation.Point()).Y
                < ServiceSection.TransformToVisual(TicketBodyScroll).TransformPoint(new Windows.Foundation.Point()).Y;
        var addFeedback = new { count = model.Cart.Count, message = model.Message, quantityText = QuantityBox.Text, saveEnabled = SaveButton.IsEnabled, canEdit = model.CanEdit };
        var menuSource = MenuGrid.ItemsSource;
        CartList.UpdateLayout();
        var increase = model.Cart.Count == 1 ? FindTicketButton(CartList, model.Cart[0].LineId, "Increase") : null;
        var quantityButton = increase is not null;
        increase?.Focus(FocusState.Keyboard);
        if (increase is not null) OnIncreaseLine(increase, new RoutedEventArgs());
        await Task.Delay(100);
        var quantity = model.Cart.Count == 1 && model.Cart[0].Quantity == 2 && CartCountText.Text == "2 ITEMS";
        var keyboardRetained = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) is Button focused
            && AutomationProperties.GetName(focused).StartsWith("Increase", StringComparison.Ordinal);
        var totalUpdated = model.Cart.Count == 1 && SubtotalText.Text == model.Cart[0].Amount && TicketTotalText.Text == model.Cart[0].Amount;
        var stableMenu = ReferenceEquals(menuSource, MenuGrid.ItemsSource);
        var originalOperator = model.Session.Operator;
        OperatorBox.SelectedItem = null;
        model.Session.Operator = null;
        OnSaveDraft(this, new RoutedEventArgs()); // Required-operator validation returns before any write.
        var operatorValidation = model.Order is null && TicketFeedback.IsOpen && model.Error?.Description.Contains("operator") == true;
        model.Session.Operator = originalOperator;
        if (model.Cart.Count == 1) model.ChangeQuantity(model.Cart[0], model.Cart[0].Quantity);
        await Task.Delay(100);
        await PresentationVerification.CaptureAsync(TicketPanel, "native-counter-ticket.png");
        CartList.UpdateLayout();
        var remove = model.Cart.Count == 1 ? FindTicketButton(CartList, model.Cart[0].LineId, "Remove") : null;
        var removeButton = remove is not null;
        if (remove is not null) OnRemoveLine(remove, new RoutedEventArgs());
        var removed = model.Cart.Count == 0 && TicketEmptyText.Visibility == Visibility.Visible && !SaveButton.IsEnabled;

        ShowCustomization(card);
        await Task.Delay(150);
        CustomizationDialog.Hide();
        await Task.Delay(150);
        var cancelled = !customizationOpen && model.Cart.Count == 0 && MenuGrid.SelectedItem is null;

        var originalSearch = model.Search;
        model.Search = "__counter_no_match_verification__";
        await model.LoadAsync();
        var emptySearch = model.Error is null && model.VisibleMenu.Count == 0
            && EmptyPanel.Visibility == Visibility.Visible && ClearSearchButton.IsEnabled;
        model.Search = originalSearch;
        await model.LoadAsync();
        ClearMenuSelection();
        var serviceForms = await VerifyServiceFormsAsync();
        bool? savedReview = null;
        bool? savedAction = null;
        var existingOrder = App.Services.GetRequiredService<OperationsWorkspace>().Orders.FirstOrDefault();
        if (existingOrder is not null)
        {
            await model.OpenAsync(existingOrder.Id); // Review an existing order; no business writes.
            savedReview = model.Order?.Id == existingOrder.Id && !model.CanEdit
                && CartList.Visibility == Visibility.Collapsed && SaveButton.Visibility == Visibility.Collapsed
                && TicketTotalText.Text == model.Order.Total.ToString("0.00");
            savedAction = ConfirmButton.Visibility == (model.Order?.Status == CoffeeShop.Domain.Sales.OrderStatus.Draft
                ? Visibility.Visible : Visibility.Collapsed);
            model.NewTicket();
            RestoreForm();
        }
        return new { serviceHiddenUntilItems, orderFirst, customization, invalidStaysOpen, cancelled, added, addFeedback, quantityButton, quantity, keyboardRetained, totalUpdated, operatorValidation, removeButton, stableMenu, removed, emptySearch, serviceForms, savedReview, savedAction, restored = model.Menu.Any(row => row.Id == item.Id) };
    }

    private async Task<object> VerifyServiceFormsAsync()
    {
        var oldChannel = model.Channel;
        var oldCustomer = model.Customer;
        var oldTable = model.TableId;
        var recipient = RecipientBox.Text;
        var contact = ContactBox.Text;
        var address = AddressBox.Text;
        var tabs = new[] { DineInTab, TakeawayTab, DeliveryTab };
        var exclusive = true;
        var styled = true;
        var forms = true;
        try
        {
            foreach (var tab in new[] { DineInTab, DeliveryTab, TakeawayTab, DineInTab, TakeawayTab })
            {
                tab.IsChecked = true; // Exercises native grouped selection and Checked handlers.
                await Task.Delay(20);
                exclusive &= tabs.Count(item => item.IsChecked == true) == 1 && model.Channel == (string)tab.Tag;
                styled &= tabs.Count(item => ReferenceEquals(item.BorderBrush, Microsoft.UI.Xaml.Application.Current.Resources["ConsoleAccentTextBrush"])) == 1;
                forms &= (TablePanel.Visibility == Visibility.Visible) == (model.Channel == "dine_in")
                    && (DeliveryPanel.Visibility == Visibility.Visible) == (model.Channel == "delivery");
            }
            DeliveryTab.IsChecked = true;
            RecipientBox.Text = "Delivery verification";
            ContactBox.Text = "0900000000";
            AddressBox.Text = "Test-only address";
            TakeawayTab.IsChecked = true;
            DeliveryTab.IsChecked = true;
            var retained = RecipientBox.Text == "Delivery verification" && ContactBox.Text == "0900000000" && AddressBox.Text == "Test-only address";
            var deliveryIndependent = DeliveryPanel.Visibility == Visibility.Visible;
            var before = model.Channel;
            DeliveryTab.IsChecked = true;
            exclusive &= model.Channel == before && DeliveryTab.IsChecked == true;
            return new { exclusive, styled, forms, retained, deliveryIndependent };
        }
        finally
        {
            model.Channel = oldChannel;
            model.Customer = oldCustomer;
            model.TableId = oldTable;
            RecipientBox.Text = recipient;
            ContactBox.Text = contact;
            AddressBox.Text = address;
            Render();
        }
    }

    internal async Task CaptureServiceFormsAsync()
    {
        var channel = model.Channel;
        PosCartLine? captureLine = null;
        if (model.Order is null && model.Cart.Count == 0 && model.Menu.Count > 0)
        {
            var item = model.Menu[0];
            model.AddLine(item, item.SizeVariants.First(size => size.IsActive), 1, []);
            captureLine = model.Cart.LastOrDefault();
        }
        try
        {
            foreach (var tab in new[] { DineInTab, TakeawayTab, DeliveryTab })
            {
                tab.IsChecked = true;
                ServiceSection.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false, VerticalAlignmentRatio = 0 });
                await Task.Delay(100);
                await PresentationVerification.CaptureAsync(TicketPanel, $"native-counter-{tab.Tag}.png");
            }
        }
        finally
        {
            if (captureLine is not null) model.RemoveLine(captureLine);
            model.Channel = channel;
            Render();
            TicketBodyScroll.ChangeView(null, 0, null, true);
        }
    }

    internal bool PrepareTicketCapture()
    {
        if (model.IsBusy || model.Order is not null || model.Cart.Count != 0 || model.Menu.Count == 0) return false;
        ticketCaptureActive = true;
        captureOperator = model.Session.Operator;
        model.Session.Operator ??= model.Options.Staff.FirstOrDefault();
        var item = model.Menu[0];
        model.AddLine(item, item.SizeVariants.First(size => size.IsActive), 2,
            item.CompatibleAddons.Where(addon => addon.IsActive).Take(1).ToArray());
        var second = model.Menu.Where(row => row.Id != item.Id).OrderByDescending(row => row.Name.Length).FirstOrDefault();
        if (second is not null) model.AddLine(second, second.SizeVariants.First(size => size.IsActive), 1, []);
        TicketBodyScroll.ChangeView(null, 0, null, true);
        return true;
    }

    internal void ClearTicketCapture()
    {
        if (!ticketCaptureActive) return;
        foreach (var line in model.Cart.ToArray()) model.RemoveLine(line);
        model.Session.Operator = captureOperator;
        ticketCaptureActive = false;
        Render();
    }
}
