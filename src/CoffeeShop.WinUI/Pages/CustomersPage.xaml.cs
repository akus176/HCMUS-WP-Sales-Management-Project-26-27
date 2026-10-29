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
    internal object VerificationState() => new { search = SearchBox.Text, selected = model.SelectedCustomer?.Id, profile = UseSelectedProfile.IsChecked, delivery = DeliveryOrder.IsChecked, recipient = RecipientName.Text, contact = ContactNumber.Text, address = OrderAddress.Text };

    internal async Task<object> PrepareVerificationAsync()
    {
        while (model.IsBusy) await Task.Delay(50);
        await model.LoadAsync();
        if (model.Customers.Count > 0) await model.SelectAsync(model.Customers[0].Id);
        return new { profiles = model.Customers.Count, selected = model.SelectedCustomer?.FullName, addresses = model.SelectedCustomer?.Addresses.Count, history = model.History.TotalCount, error = model.Error?.Code };
    }

    private readonly CustomerWorkspace model;
    private bool rendering;
    private bool ready;
    private EntityId? renderedCustomerId;
    public sealed record CustomerRow(CustomerSummary Customer)
    {
        public EntityId Id => Customer.Id;
        public string FullName => Customer.FullName;
        public string Phone => Customer.Phone ?? "";
        public int LoyaltyPointsBalance => Customer.LoyaltyPointsBalance;
        public string Initials => InitialsFor(FullName);
    }
    public sealed record AddressRow(CustomerAddressSummary Address)
    {
        public string Label => Address.Label ?? "Saved address";
        public string AddressLine => Address.AddressLine;
        public bool IsActive => Address.IsActive;
        public string StateDisplay => Address.IsDefault ? "DEFAULT" : Address.IsActive ? "ACTIVE" : "INACTIVE";
    }
    private static string InitialsFor(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(part => part[0]));

    public CustomersPage()
    {
        InitializeComponent();
        model = App.Services.GetRequiredService<CustomerWorkspace>();
        renderedCustomerId = model.SelectedCustomer?.Id;
        SearchBox.Text = model.Search;
        IncludeInactive.IsChecked = model.IncludeInactive;
        ready = true;
        RestoreOrderForm();
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
            LoyaltyTransactionSummary transaction => $"{transaction.Type}, {transaction.Points} points, balance {transaction.BalanceAfter}",
            CustomerOrderSummary order => $"{order.OrderNumber}, {order.Status}, total {order.Total:N2}",
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
            NewCustomerButton.IsEnabled = !model.RequiresReload;
            Status.Apply(model.Feedback);
            if (model.Feedback.Kind == WorkflowFeedbackKind.Ready) Status.IsOpen = false;
            ProfileCount.Text = $"MANAGE / CUSTOMER NOTEBOOK / {model.Customers.Count} profiles";
            EmptyMessage.Visibility = model.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
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
            ProfileInitials.Text = customer is null ? "" : InitialsFor(customer.FullName);
            ContactDetails.Text = customer is null ? "" : $"{customer.Phone} · {customer.Email}{(customer.IsActive ? "" : " · Inactive")}";
            BalanceText.Text = customer is null ? "" : $"{customer.LoyaltyPointsBalance:N0} pts";
            ProfileActions.Visibility = customer is null ? Visibility.Collapsed : Visibility.Visible;
            EditCustomerButton.IsEnabled = model.CanEdit;
            DeactivateCustomerButton.IsEnabled = model.CanEdit;
            AddAddressButton.IsEnabled = model.CanEdit;
            AddressesList.IsEnabled = model.CanEdit;
            AddressesList.ItemsSource = customer?.Addresses.Select(address => new AddressRow(address)).ToArray();
            AddressesEmpty.Visibility = customer is not null && customer.Addresses.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryList.ItemsSource = model.History.Items.Select(item => item with
            {
                Type = item.Type switch
                {
                    "earn" => "Points earned",
                    "redeem" => "Points redeemed",
                    "reverse_earn" => "Earning reversed",
                    "reverse_redeem" => "Redemption restored",
                    _ => item.Type
                }
            }).ToArray();
            HistoryEmpty.Visibility = customer is not null && model.History.TotalCount == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryPageText.Text = customer is null || model.History.TotalCount == 0 ? ""
                : $"Transactions {model.History.Offset + 1}–{model.History.Offset + model.History.Items.Count} of {model.History.TotalCount}";
            PreviousButton.IsEnabled = model.CanPrevious;
            NextButton.IsEnabled = model.CanNext;
            OrdersList.ItemsSource = customer?.RecentOrders;
            OrdersEmpty.Visibility = customer is not null && customer.RecentOrders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UseSelectedProfile.IsEnabled = model.CanEdit;
            UseSelectedProfile.Content = customer is null ? "Select an active profile above to attach it" : $"Attach {customer.FullName}";
            SelectForOrderButton.IsEnabled = !model.RequiresReload;
            OrderSummary.Text = customer is null ? "Walk-in — no profile required." : $"Use {customer.FullName.Split(' ')[0]} for the next ticket";
            if (model.OrderSelection.CustomerId == customer?.Id && model.OrderSelection.Delivery is { } delivery)
            {
                OrderSummary.Text += $"\nDelivery to {delivery.RecipientName} · {delivery.ContactNumber}\n{model.OrderSelection.AddressLine}";
            }

            if (!model.IsBusy)
            {
                if (customer?.IsActive != true)
                {
                    UseSelectedProfile.IsChecked = false;
                }

                if (renderedCustomerId != customer?.Id)
                {
                    renderedCustomerId = customer?.Id;
                    UseSelectedProfile.IsChecked = customer?.IsActive == true;
                    RecipientName.Text = customer?.FullName ?? "";
                    ContactNumber.Text = customer?.Phone ?? "";
                }

                UpdateAddressChoices();
            }
        }
        finally
        {
            rendering = false;
        }

        UpdateOrderOptions();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (ListColumn is null) return;
        var wide = ActualWidth >= 1000;
        ListColumn.Width = wide ? new GridLength(280) : new GridLength(1, GridUnitType.Star);
        DetailColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        OrderColumn.Width = new GridLength(wide ? 282 : 0);
        Grid.SetColumn(DetailPanel, wide ? 1 : 0);
        Grid.SetRow(DetailPanel, wide ? 0 : 1);
        Grid.SetColumn(OrderPanel, wide ? 2 : 0);
        Grid.SetRow(OrderPanel, wide ? 0 : 2);
        HistoryColumn.Width = wide && ActualWidth >= 1200 ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        Grid.SetColumn(HistoryPanel, wide && ActualWidth >= 1200 ? 1 : 0);
        Grid.SetRow(HistoryPanel, wide && ActualWidth >= 1200 ? 0 : 1);
        HeaderOrnament.Visibility = ActualWidth >= 850 ? Visibility.Visible : Visibility.Collapsed;
        Header.ColumnDefinitions[1].Width = new GridLength(ActualWidth >= 850 ? 116 : 0);
        HeaderActions.Orientation = ActualWidth >= 700 ? Orientation.Horizontal : Orientation.Vertical;
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
        var defaultAddress = new CheckBox { Content = "Default delivery address", IsChecked = address?.IsDefault == true, MinHeight = 48 };
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

    private void OnWalkIn(object sender, RoutedEventArgs args)
    {
        model.ContinueAsWalkIn();
        RestoreOrderForm();
    }

    private void OnOrderOptionsChanged(object sender, RoutedEventArgs args)
    {
        if (ready && !rendering)
        {
            UpdateAddressChoices();
            UpdateOrderOptions();
        }
    }

    private void OnAddressChoiceChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ready && !rendering)
        {
            UpdateOrderOptions();
        }
    }

    private void UpdateAddressChoices()
    {
        var selectedAddress = (AddressChoice.SelectedItem as AddressOption)?.Id;
        var options = new List<AddressOption> { new(null, "Order-only address") };
        if (UseSelectedProfile.IsChecked == true && model.SelectedCustomer is { IsActive: true } customer)
        {
            options.AddRange(customer.Addresses.Where(item => item.IsActive).Select(item =>
                new AddressOption(item.Id, $"{item.Label ?? "Address"}{(item.IsDefault ? " (default)" : "")}: {item.AddressLine}")));
        }

        AddressChoice.ItemsSource = options;
        AddressChoice.SelectedItem = options.FirstOrDefault(item => item.Id == selectedAddress) ?? options[0];
    }

    private void UpdateOrderOptions()
    {
        DeliveryFields.Visibility = DeliveryOrder.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        OrderAddress.IsEnabled = (AddressChoice.SelectedItem as AddressOption)?.Id is null;
    }

    private void RestoreOrderForm()
    {
        rendering = true;
        var request = model.OrderRequest;
        UseSelectedProfile.IsChecked = request.CustomerId is not null;
        DeliveryOrder.IsChecked = request.IsDelivery;
        RecipientName.Text = request.RecipientName ?? "";
        ContactNumber.Text = request.ContactNumber ?? "";
        OrderAddress.Text = request.AddressLine ?? "";
        UpdateAddressChoices();
        if (AddressChoice.ItemsSource is IEnumerable<AddressOption> options)
        {
            AddressChoice.SelectedItem = options.FirstOrDefault(item => item.Id == request.CustomerAddressId) ?? options.First();
        }

        rendering = false;
        UpdateOrderOptions();
    }

    private async void OnSelectForOrder(object sender, RoutedEventArgs args)
    {
        var attach = UseSelectedProfile.IsChecked == true;
        if (attach && model.SelectedCustomer is not { IsActive: true })
        {
            model.ShowValidation("Select an active customer, or uncheck profile attachment to continue as a walk-in.");
            return;
        }

        var delivery = DeliveryOrder.IsChecked == true;
        await model.SetOrderSelectionAsync(new(
            attach ? model.SelectedCustomer!.Id : null,
            delivery,
            delivery ? (AddressChoice.SelectedItem as AddressOption)?.Id : null,
            delivery ? RecipientName.Text : null,
            delivery ? ContactNumber.Text : null,
            delivery ? OrderAddress.Text : null));
    }

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
            Style = (Style)Resource("WorkspaceDialog"),
            PrimaryButtonText = button,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = ElementTheme.Light,
            FontFamily = (FontFamily)Resource("ConsoleSansFont"),
            Content = new ScrollViewer { Content = Form(error, progress, editor), MaxHeight = 520 }
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

    private sealed record AddressOption(EntityId? Id, string DisplayName);
}
