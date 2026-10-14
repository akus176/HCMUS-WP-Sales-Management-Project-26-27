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
    private readonly CustomerWorkspace model;
    private bool rendering;
    private bool ready;
    private EntityId? renderedCustomerId;

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
        if (!rendering && CustomersList.SelectedItem is CustomerSummary customer)
        {
            await model.SelectAsync(customer.Id);
        }
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        var name = args.Item switch
        {
            CustomerSummary customer => $"{customer.FullName}, {customer.Phone}, {(customer.IsActive ? "active" : "inactive")}",
            CustomerAddressSummary address => $"{address.Label}, {address.AddressLine}",
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
            Status.Severity = model.IsError ? InfoBarSeverity.Error : InfoBarSeverity.Informational;
            Status.Title = model.Error?.Type.ToString() ?? "Customer notebook";
            Status.Message = StatusMessage();
            EmptyMessage.Visibility = model.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            var customer = model.SelectedCustomer;
            if (!ReferenceEquals(CustomersList.ItemsSource, model.Customers))
            {
                CustomersList.ItemsSource = model.Customers;
                CustomersList.SelectedItem = model.Customers.FirstOrDefault(item => item.Id == customer?.Id);
            }

            DetailHeading.Text = customer is null
                ? "Select a customer to open their notebook."
                : $"{customer.FullName} ({(customer.IsActive ? "active" : "inactive")})";
            ContactDetails.Text = customer is null ? "" : $"{customer.Phone}\n{customer.Email}";
            BalanceText.Text = customer is null ? "" : $"{customer.LoyaltyPointsBalance:N0} loyalty points";
            ProfileActions.Visibility = customer is null ? Visibility.Collapsed : Visibility.Visible;
            EditCustomerButton.IsEnabled = model.CanEdit;
            DeactivateCustomerButton.IsEnabled = model.CanEdit;
            AddAddressButton.IsEnabled = model.CanEdit;
            AddressesList.IsEnabled = model.CanEdit;
            AddressesList.ItemsSource = customer?.Addresses;
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
            OrderSummary.Text = model.OrderSelection.CustomerName ?? "Walk-in — no profile required.";
            if (model.OrderSelection.Delivery is { } delivery)
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
        await EditAddressAsync((CustomerAddressSummary)((Button)sender).Tag);

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
        if (model.SelectedCustomer is { IsActive: true } customer && ((Button)sender).Tag is CustomerAddressSummary address)
        {
            await model.SetDefaultAddressAsync(customer.Id, address.Id);
        }
    }

    private async void OnDeactivateAddress(object sender, RoutedEventArgs args)
    {
        if (model.SelectedCustomer is { IsActive: true } customer && ((Button)sender).Tag is CustomerAddressSummary address)
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
            Title = title,
            PrimaryButtonText = button,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            RequestedTheme = ElementTheme.Light,
            FontFamily = (FontFamily)Resource("SketchBodyFontFamily"),
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
        Style = (Style)Resource("SketchCustomerFieldStyle")
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
