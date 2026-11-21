using System.ComponentModel;
using CoffeeShop.Application.Customers;
using CoffeeShop.Domain.Common;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class CustomersPage : Page
{
    internal object VerificationState() => new { search = SearchBox.Text, selected = model.SelectedCustomer?.Id };

    internal async Task<object> PrepareVerificationAsync()
    {
        while (model.IsBusy) await Task.Delay(50);
        await model.LoadAsync();
        if (model.Customers.Count > 0) await model.SelectAsync(model.Customers[0].Id);
        return new { profiles = model.Customers.Count, selected = model.SelectedCustomer?.FullName, addresses = model.SelectedCustomer?.Addresses.Count, history = model.History.TotalCount, error = model.Error?.Code };
    }

    internal object VerificationLayout() => new
    {
        threeColumns = Grid.GetColumn(DetailScroll) == 1 && Grid.GetColumn(RecentOrdersScroll) == 2,
        contentFits = CustomerLayout.ActualWidth <= ActualWidth - 47,
        directoryFits = DirectoryPanel.ActualWidth <= ActualWidth - 47,
        detailFits = DetailScroll.ActualWidth <= ActualWidth - 47,
        orderFits = RecentOrdersScroll.ActualWidth <= ActualWidth - 47,
        independentScrolling = WorkspaceContent.VerticalScrollMode == ScrollMode.Disabled,
        listHeight = CustomersList.ActualHeight
    };

    internal async Task<object> VerifyCustomerInteractionsAsync(Func<int, Task> resize)
    {
        var originalSearch = SearchBox.Text;
        SearchBox.Text = "__verification_no_customer_matches__";
        await SearchAsync();
        var emptySearchWorks = model.IsEmpty && EmptyState.Visibility == Visibility.Visible && CustomersList.Items.Count == 0;
        SearchBox.Text = originalSearch;
        await SearchAsync();
        var searchRecovers = CustomersList.Items.Count == model.Customers.Count && EmptyState.Visibility == Visibility.Collapsed;
        await Task.Delay(350);
        var directoryScroll = PresentationVerification.Elements(CustomersList).OfType<ScrollViewer>().FirstOrDefault();
        var profilePosition = DetailScroll.TransformToVisual(this).TransformPoint(new Windows.Foundation.Point());
        var independentDirectoryScroll = directoryScroll is not null;
        if (directoryScroll is { ScrollableHeight: > 0 })
        {
            directoryScroll.ChangeView(null, Math.Min(150, directoryScroll.ScrollableHeight), null, true);
            await Task.Delay(150);
            independentDirectoryScroll = directoryScroll.VerticalOffset > 0
                && DetailScroll.TransformToVisual(this).TransformPoint(new Windows.Foundation.Point()) == profilePosition;
            directoryScroll.ChangeView(null, 0, null, true);
        }

        object editor = new { skipped = "No active customer available" };
        if (model.SelectedCustomer is { IsActive: true } customer)
        {
            editorVerification = async (dialog, fields) =>
            {
                var name = fields.Children.OfType<TextBox>().First();
                var savedName = name.Text;
                name.Text = savedName + " unsaved draft";
                await resize(600);
                var compactPreserved = name.Text == savedName + " unsaved draft";
                await resize(1440);
                if (PresentationVerification.Elements(dialog).OfType<Border>().FirstOrDefault(border => border.Name == "BackgroundElement") is { } frame)
                    await PresentationVerification.CaptureAsync(frame, "native-customers-editor.png");
                editor = new { compactPreserved, expandedPreserved = name.Text == savedName + " unsaved draft", savedRecordUnchanged = model.SelectedCustomer?.FullName == savedName };
                dialog.Hide();
            };
            try { await EditCustomerAsync(customer); }
            finally { editorVerification = null; }
        }
        return new { emptySearchWorks, searchRecovers, independentDirectoryScroll, editor };
    }

    private readonly CustomerWorkspace model;
    private bool rendering;
    private bool ready;
    private Func<ContentDialog, StackPanel, Task>? editorVerification;
    public sealed record CustomerRow(CustomerSummary Customer)
    {
        public EntityId Id => Customer.Id;
        public string FullName => Customer.FullName;
        public string Phone => Customer.Phone ?? "";
        public string ContactDisplay => Phone + (Customer.IsActive ? "" : " · Inactive");
        public int LoyaltyPointsBalance => Customer.LoyaltyPointsBalance;
        public string Initials => InitialsFor(FullName);
    }
    public sealed record AddressRow(CustomerAddressSummary Address)
    {
        public string Label => Address.Label ?? "Saved address";
        public string AddressLine => Address.AddressLine;
        public bool IsActive => Address.IsActive;
        public string StateDisplay => !Address.IsActive ? "Inactive" : Address.IsDefault ? "Default address" : "Active";
    }
    public sealed record LoyaltyRow(LoyaltyTransactionSummary Transaction)
    {
        public string Type => Transaction.Type switch
        {
            "earn" => "Points earned",
            "redeem" => "Points redeemed",
            "reverse_earn" => "Earning reversed",
            "reverse_redeem" => "Redemption restored",
            _ => Transaction.Type
        };
        public string DateDisplay => Transaction.CreatedAt.ToLocalTime().ToString("dd MMM yyyy · HH:mm");
        public string OrderDisplay => (Transaction.OrderId is { } id ? $"Order {id.ToString()[..8].ToUpperInvariant()}" : "No linked order")
            + (Transaction.ReversesTransactionId is { } reversal ? $"\nReverses {reversal.ToString()[..8].ToUpperInvariant()}" : "");
        public string ReferenceDetails => $"Order: {Transaction.OrderId}\nReverses: {Transaction.ReversesTransactionId}";
        public string PointsDisplay => Transaction.Points.ToString("+0;-0;0");
        public string BalanceDisplay => $"Balance {Transaction.BalanceAfter:N0}";
    }
    public sealed record RecentOrderRow(CustomerOrderSummary Order)
    {
        public string OrderNumber => Order.OrderNumber;
        public string StatusDisplay => System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Order.Status.Replace('_', ' '));
        public string DateDisplay => Order.CreatedAt.ToLocalTime().ToString("dd MMM yyyy · HH:mm");
        public string TotalDisplay => Order.Total.ToString("N2");
    }
    private static string InitialsFor(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[0]));

    public CustomersPage()
    {
        InitializeComponent();
        model = App.Services.GetRequiredService<CustomerWorkspace>();
        SearchBox.Text = model.Search;
        IncludeInactive.IsChecked = model.IncludeInactive;
        ready = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        model.PropertyChanged -= OnModelChanged;
        model.PropertyChanged += OnModelChanged;
        await model.LoadAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => model.PropertyChanged -= OnModelChanged;

    private void OnModelChanged(object? sender, PropertyChangedEventArgs args) => Render();

    private async void OnReload(object sender, RoutedEventArgs args) => await model.LoadAsync();

    private async void OnSearch(object sender, RoutedEventArgs args) => await SearchAsync();

    private void OnSearchBoxLoaded(object sender, RoutedEventArgs args)
    {
        // The native TextBox template does not bind VerticalContentAlignment.
        foreach (var part in PresentationVerification.Elements(SearchBox))
        {
            if (part is ScrollViewer { Name: "ContentElement" } content)
            {
                content.VerticalAlignment = VerticalAlignment.Center;
                content.VerticalContentAlignment = VerticalAlignment.Center;
            }
            if (part is TextBlock { Name: "PlaceholderTextContentPresenter" } placeholder)
            {
                placeholder.Style = null;
                placeholder.FontFamily = SearchBox.FontFamily;
                placeholder.FontSize = SearchBox.FontSize;
                placeholder.LineHeight = 0;
                placeholder.VerticalAlignment = VerticalAlignment.Center;
            }
        }
    }

    private async void OnInactiveChanged(object sender, RoutedEventArgs args)
    {
        if (ready && !rendering && !model.IsBusy) await SearchAsync();
    }

    private async void OnClearSearch(object sender, RoutedEventArgs args)
    {
        SearchBox.Text = "";
        await SearchAsync();
    }

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key == Windows.System.VirtualKey.Enter)
        {
            args.Handled = true;
            await SearchAsync();
        }
    }

    private Task SearchAsync()
    {
        model.Search = SearchBox.Text;
        model.IncludeInactive = IncludeInactive.IsChecked == true;
        return model.LoadAsync();
    }

    private async void OnCustomerSelected(object sender, SelectionChangedEventArgs args)
    {
        if (!rendering && CustomersList.SelectedItem is CustomerRow { Customer: var customer })
        {
            await model.SelectAsync(customer.Id);
        }
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var name = args.Item switch
        {
            CustomerRow { Customer: var customer } => $"{customer.FullName}, {customer.Phone}, {(customer.IsActive ? "active" : "inactive")}",
            AddressRow { Address: var address } => $"{address.Label}, {address.AddressLine}, {(address.IsDefault ? "default" : "")}",
            LoyaltyRow { Transaction: var transaction } => $"{transaction.Type}, {transaction.Points} points, balance {transaction.BalanceAfter}, order {transaction.OrderId}, reverses {transaction.ReversesTransactionId}",
            RecentOrderRow { Order: var order } => $"{order.OrderNumber}, {order.Status}, total {order.Total:N2}",
            _ => ""
        };
        AutomationProperties.SetName(args.ItemContainer, name);
    }

    private void Render()
    {
        rendering = true;
        try
        {
            LoadingBar.Visibility = model.IsBusy ? Visibility.Visible : Visibility.Collapsed;
            WorkspaceContent.IsEnabled = !model.IsBusy;
            ReloadButton.IsEnabled = !model.IsBusy;
            NewCustomerButton.IsEnabled = !model.IsBusy && !model.RequiresReload;
            Status.Apply(model.Feedback);
            if (model.Feedback.Kind == WorkflowFeedbackKind.Ready) Status.IsOpen = false;
            ResultsCount.Text = model.Customers.Count == 100 ? "100+ matches" : $"{model.Customers.Count} profiles";
            EmptyState.Visibility = model.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            var customer = model.SelectedCustomer;
            if (!ReferenceEquals(CustomersList.Tag, model.Customers))
            {
                CustomersList.Tag = model.Customers;
                CustomersList.ItemsSource = model.Customers.Select(item => new CustomerRow(item)).ToArray();
            }
            CustomersList.SelectedItem = ((IEnumerable<CustomerRow>)CustomersList.ItemsSource).FirstOrDefault(item => item.Id == customer?.Id);

            DetailHeading.Text = customer is null
                ? "Select a customer"
                : customer.FullName;
            ContactDetails.Text = customer is null ? "Choose a profile from the directory to view its details."
                : string.Join("\n", new[] { customer.Phone, customer.Email, customer.IsActive ? null : "Inactive profile" }.Where(value => !string.IsNullOrWhiteSpace(value)));
            BalanceText.Text = customer is null ? "—" : $"{customer.LoyaltyPointsBalance:N0} points";
            ProfileActions.Visibility = customer is null ? Visibility.Collapsed : Visibility.Visible;
            EditCustomerButton.IsEnabled = model.CanEdit;
            DeactivateCustomerButton.IsEnabled = model.CanEdit;
            AddAddressButton.IsEnabled = model.CanEdit;
            AddressesList.IsEnabled = model.CanEdit;
            AddressesList.ItemsSource = customer?.Addresses.Select(address => new AddressRow(address)).ToArray();
            AddressesEmpty.Visibility = customer is not null && customer.Addresses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryList.ItemsSource = model.History.Items.Select(item => new LoyaltyRow(item)).ToArray();
            HistoryEmpty.Visibility = customer is not null && model.History.TotalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryPageText.Text = customer is null || model.History.TotalCount == 0 ? ""
                : $"Transactions {model.History.Offset + 1}–{model.History.Offset + model.History.Items.Count} of {model.History.TotalCount}";
            HistoryPageText.Visibility = customer is not null && model.History.TotalCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryPagination.Visibility = customer is not null && model.History.TotalCount > model.History.Limit
                ? Visibility.Visible : Visibility.Collapsed;
            PreviousButton.IsEnabled = model.CanPrevious;
            NextButton.IsEnabled = model.CanNext;
            OrdersList.ItemsSource = customer?.RecentOrders.Select(order => new RecentOrderRow(order)).ToArray();
            OrdersEmpty.Text = customer is null ? "Select a customer to see recent orders." : "No recent orders.";
            OrdersEmpty.Visibility = customer is null || customer.RecentOrders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            rendering = false;
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (ListColumn is null) return;
        var wide = ActualWidth >= 1000;
        var height = Math.Max(260, WorkspaceContent.ActualHeight);
        WorkspaceContent.VerticalScrollMode = wide ? ScrollMode.Disabled : ScrollMode.Enabled;
        WorkspaceContent.VerticalScrollBarVisibility = wide ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        CustomerLayout.Height = wide ? height : double.NaN;
        CustomerLayout.RowDefinitions[0].Height = wide ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        DirectoryPanel.Height = wide ? height : Math.Clamp(height * .7, 260, 480);
        DirectoryPanel.BorderThickness = wide ? new Thickness(0, 0, 1, 0) : new Thickness(0, 0, 0, 1);
        DirectoryPanel.Padding = wide ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 12);
        DetailScroll.MaxHeight = RecentOrdersScroll.MaxHeight = wide ? height : double.PositiveInfinity;
        ListColumn.Width = wide ? new GridLength(280) : new GridLength(1, GridUnitType.Star);
        DetailColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        RecentOrdersColumn.Width = new GridLength(wide ? 282 : 0);
        CustomerLayout.ColumnSpacing = wide ? 18 : 0;
        CustomerLayout.RowSpacing = wide ? 0 : 16;
        Grid.SetColumn(DetailScroll, wide ? 1 : 0);
        Grid.SetRow(DetailScroll, wide ? 0 : 1);
        Grid.SetColumn(RecentOrdersScroll, wide ? 2 : 0);
        Grid.SetRow(RecentOrdersScroll, wide ? 0 : 2);
        var historyAtSide = wide && ActualWidth >= 1200;
        HistoryColumn.Width = historyAtSide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(HistoryPanel, historyAtSide ? 1 : 0);
        Grid.SetRow(HistoryPanel, historyAtSide ? 0 : 1);
        var headerInline = ActualWidth >= 560;
        Header.ColumnDefinitions[1].Width = headerInline ? GridLength.Auto : new GridLength(0);
        Header.ColumnSpacing = headerInline ? 18 : 0;
        Grid.SetColumn(HeaderActions, headerInline ? 1 : 0);
        Grid.SetColumnSpan(HeaderActions, headerInline ? 1 : 2);
        Grid.SetRow(HeaderActions, headerInline ? 0 : 1);
        CustomerTitle.FontSize = headerInline ? 42 : 34;
    }

    private string StatusMessage() => model.Error is { Details.Count: > 0 } error
        ? string.Join("\n", error.Details.Select(detail => $"{FieldName(detail.Target)}: {FriendlyMessage(detail.Message)}"))
        : FriendlyMessage(model.Message);

    private static string FriendlyMessage(string message) => message.Split(" (Parameter '")[0];

    private static string FieldName(string field) => field switch
    {
        "fullName" => "Full name",
        "phone" => "Phone",
        "email" => "Email",
        "addressLine" => "Address",
        "recipientName" => "Recipient name",
        "contactNumber" => "Contact number",
        "label" => "Label",
        "query" => "Search",
        _ => "Customer details"
    };

    private async void OnNewCustomer(object sender, RoutedEventArgs args) => await EditCustomerAsync(null);

    private async void OnEditCustomer(object sender, RoutedEventArgs args)
    {
        if (model.SelectedCustomer is { IsActive: true } customer)
        {
            await EditCustomerAsync(customer);
        }
    }

    private Task EditCustomerAsync(CustomerDetail? customer)
    {
        var name = Field("Full name", customer?.FullName, 120);
        var phone = Field("Phone", customer?.Phone, 30);
        var email = Field("Email (optional)", customer?.Email, 254);
        return ShowEditorAsync(customer is null ? "New customer" : "Edit customer", Form(name, phone, email), () => customer is null
            ? model.CreateAsync(new(name.Text, phone.Text, email.Text))
            : model.UpdateAsync(new(customer.Id, name.Text, phone.Text, email.Text)));
    }

    private async void OnDeactivateCustomer(object sender, RoutedEventArgs args)
    {
        if (model.SelectedCustomer is { IsActive: true } customer)
        {
            await ConfirmAsync($"Deactivate {customer.FullName}?",
                "The profile will be unavailable for new orders. Its addresses, order history, and loyalty history remain available.",
                () => model.DeactivateAsync(customer.Id));
        }
    }

    private async void OnAddAddress(object sender, RoutedEventArgs args) => await EditAddressAsync(null);

    private async void OnEditAddress(object sender, RoutedEventArgs args) =>
        await EditAddressAsync((CustomerAddressSummary)((FrameworkElement)sender).Tag);

    private Task EditAddressAsync(CustomerAddressSummary? address)
    {
        if (model.SelectedCustomer is not { IsActive: true } customer)
        {
            return Task.CompletedTask;
        }

        var label = Field("Label (optional)", address?.Label, 80);
        var line = Field("Address", address?.AddressLine, 500);
        line.AcceptsReturn = true;
        line.TextWrapping = TextWrapping.Wrap;
        var defaultAddress = new CheckBox { Content = "Default delivery address", IsChecked = address?.IsDefault == true, MinHeight = 44, FontSize = 14 };
        return ShowEditorAsync(address is null ? "Add delivery address" : "Edit delivery address", Form(label, line, defaultAddress), () => address is null
            ? model.AddAddressAsync(new(customer.Id, line.Text, label.Text, defaultAddress.IsChecked == true))
            : model.UpdateAddressAsync(new(customer.Id, address.Id, line.Text, label.Text, defaultAddress.IsChecked == true)));
    }

    private async void OnDefaultAddress(object sender, RoutedEventArgs args)
    {
        if (model.SelectedCustomer is { IsActive: true } customer && ((FrameworkElement)sender).Tag is CustomerAddressSummary address)
        {
            await model.SetDefaultAddressAsync(customer.Id, address.Id);
        }
    }

    private async void OnDeactivateAddress(object sender, RoutedEventArgs args)
    {
        if (model.SelectedCustomer is { IsActive: true } customer && ((FrameworkElement)sender).Tag is CustomerAddressSummary address)
        {
            await ConfirmAsync("Deactivate this address?", "Existing deliveries keep their saved address. This address will be unavailable for new orders.",
                () => model.DeactivateAddressAsync(customer.Id, address.Id));
        }
    }

    private async void OnPreviousHistory(object sender, RoutedEventArgs args) => await model.PreviousHistoryAsync();
    private async void OnNextHistory(object sender, RoutedEventArgs args) => await model.NextHistoryAsync();

    private Task ConfirmAsync(string title, string explanation, Func<Task<bool>> action) =>
        ShowEditorAsync(title, Form(new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap }), action, "Deactivate");

    private async Task ShowEditorAsync(string title, StackPanel fields, Func<Task<bool>> save, string button = "Save")
    {
        var error = new InfoBar { IsOpen = false, IsClosable = false, Severity = InfoBarSeverity.Error };
        var progress = new ProgressBar { IsIndeterminate = true, Visibility = Visibility.Collapsed };
        var editor = new ContentControl { Content = fields, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var dialog = new ContentDialog
        {
            Title = new TextBlock { Text = title, FontFamily = (FontFamily)Resource("ConsoleSerifFont"), FontSize = 32, TextWrapping = TextWrapping.Wrap },
            Style = (Style)Resources["CustomerDialog"],
            PrimaryButtonText = button,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = ElementTheme.Light,
            FontFamily = (FontFamily)Resource("ConsoleSansFont"),
            FontSize = 14,
            Content = new ScrollViewer { Content = Form(error, progress, editor), MaxHeight = 520 }
        };
        dialog.Opened += (_, _) =>
        {
            if (PresentationVerification.Elements(dialog).OfType<Button>().FirstOrDefault(button => button.Name == "PrimaryButton") is { } primary)
            {
                // The native dialog replaces the default button style in its visual state.
                primary.Background = (Brush)Resource("ConsoleFieldBrush");
                primary.Foreground = (Brush)Resource("ConsoleOnFieldBrush");
                primary.BorderBrush = (Brush)Resource("ConsoleFieldBrush");
                primary.CornerRadius = new CornerRadius(0);
                primary.MinHeight = 44;
                primary.FontSize = 13;
                primary.Resources["AccentButtonBackgroundPointerOver"] = Resource("ConsoleFieldDeepBrush");
                primary.Resources["AccentButtonBackgroundPressed"] = Resource("ConsoleFieldDeepBrush");
            }
        };
        if (editorVerification is { } verify) dialog.Opened += async (_, _) => await verify(dialog, fields);
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
                args.Cancel = !await save();
                if (args.Cancel)
                {
                    error.Message = StatusMessage();
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

    private static TextBox Field(string header, string? value, int maxLength) => new()
    {
        Header = header,
        Text = value ?? "",
        MaxLength = maxLength,
        FontSize = 14,
        MinHeight = 44,
        Style = (Style)Resource("ConsoleFieldStyle")
    };

    private static object Resource(string key) => Microsoft.UI.Xaml.Application.Current.Resources[key];

    private static StackPanel Form(params UIElement[] elements)
    {
        var panel = new StackPanel { Spacing = 12 };
        foreach (var element in elements)
        {
            panel.Children.Add(element);
        }

        return panel;
    }
}
