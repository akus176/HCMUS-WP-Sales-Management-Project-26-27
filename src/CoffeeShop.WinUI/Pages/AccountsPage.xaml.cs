using CoffeeShop.Contracts;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class AccountsPage : Page
{
    private readonly ApiStaffAccountsSource accounts;
    private bool busy;

    public AccountsPage()
    {
        if (!App.IsApiMode || !App.Services.GetRequiredService<PosOperatorSession>().HasPermission("users.manage"))
            throw new UnauthorizedAccessException("Accounts requires an administrator session.");
        InitializeComponent();
        accounts = App.Services.GetRequiredService<ApiStaffAccountsSource>();
        Loaded += OnLoaded;
    }

    public sealed record AccountRow(string FullName, string Identity, string RoleAndState)
    {
        public static AccountRow From(StaffAccountResponse account) => new(
            account.FullName, $"{account.Username} · {account.Email}",
            $"{string.Join(", ", account.Roles)} · {(account.IsActive ? "Active" : "Inactive")}");
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await RefreshAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        if (busy) return;
        SetBusy(true, "Loading staff accounts…");
        try
        {
            var results = await accounts.SearchAsync(SearchBox.Text);
            ShowAccounts(results);
        }
        catch (ApiException exception)
        {
            ListStatus.Text = "The directory could not be loaded. Retry the search.";
            ShowFailure(exception);
        }
        catch (OperationCanceledException)
        {
            ListStatus.Text = "The directory request timed out. Retry the search.";
            ShowFeedback(InfoBarSeverity.Warning, "Request timed out", ListStatus.Text);
        }
        finally { SetBusy(false); }
    }

    private async void OnCreate(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var request = new CreateStaffRequest(FullNameBox.Text, UsernameBox.Text, EmailBox.Text,
            PhoneBox.Text, (RoleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), InitialPasswordBox.Password);
        var validation = StaffAccountForm.Validate(request);
        ValidationText.Text = validation ?? string.Empty;
        ValidationText.Visibility = validation is null ? Visibility.Collapsed : Visibility.Visible;
        if (validation is not null)
        {
            ShowFeedback(InfoBarSeverity.Warning, "Check staff details", validation);
            return;
        }

        SetBusy(true, "Creating staff account…");
        try
        {
            var created = await accounts.CreateAsync(request);
            // Clear the only in-memory copy held by the password control after a successful write.
            InitialPasswordBox.Password = string.Empty;
            FullNameBox.Text = UsernameBox.Text = EmailBox.Text = PhoneBox.Text = string.Empty;
            SearchBox.Text = string.Empty;
            try
            {
                var firstPage = await accounts.SearchAsync(string.Empty);
                var display = StaffAccountDirectory.IncludeCreated(created, firstPage);
                ShowAccounts(display);
                if (display.Length > 100)
                    ListStatus.Text = "Showing the new account plus the first 100 directory entries. Search to find others.";
                ShowFeedback(InfoBarSeverity.Success, "Staff account created",
                    $"{created.FullName} can now sign in with the assigned role.");
            }
            catch (ApiException)
            {
                ListStatus.Text = "The directory could not be refreshed. Search again to see the new account.";
                ShowFeedback(InfoBarSeverity.Warning, "Account created", ListStatus.Text);
            }
        }
        catch (ApiException exception) { ShowFailure(exception); }
        catch (OperationCanceledException)
        {
            ShowFeedback(InfoBarSeverity.Warning, "Request timed out",
                "Check the directory before trying again; the account may have been created.");
        }
        finally { SetBusy(false); }
    }

    private void ShowAccounts(StaffAccountResponse[] results)
    {
        AccountsList.ItemsSource = results.Select(AccountRow.From).ToArray();
        ListStatus.Text = results.Length == 0 ? "No staff accounts match this search." :
            results.Length >= 100 ? "Showing the first 100 staff accounts. Search to find others." :
            $"Showing {results.Length} staff account{(results.Length == 1 ? string.Empty : "s")}.";
    }

    private void SetBusy(bool value, string? message = null)
    {
        busy = value;
        RefreshButton.IsEnabled = CreateButton.IsEnabled = !value;
        ListProgress.IsActive = value;
        ListProgress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        if (message is not null) ListStatus.Text = message;
    }

    private void ShowFailure(ApiException exception)
    {
        switch (exception.Kind)
        {
            case ApiErrorKind.Validation:
                ShowFeedback(InfoBarSeverity.Warning, "Check staff details", "Correct the account details and try again.");
                break;
            case ApiErrorKind.Conflict:
                ShowFeedback(InfoBarSeverity.Error, "Account already exists", "That username or email is already in use.");
                break;
            case ApiErrorKind.Forbidden:
                ShowFeedback(InfoBarSeverity.Error, "Administrator access required", "Your current role cannot create or view staff accounts.");
                break;
            case ApiErrorKind.Unauthorized:
                ShowFeedback(InfoBarSeverity.Error, "Sign in required", "Your session has expired. Sign in again.");
                break;
            default:
                ShowFeedback(InfoBarSeverity.Error, "Could not complete request", "Check the connection and try again.");
                break;
        }
    }

    private void ShowFeedback(InfoBarSeverity severity, string title, string message)
    {
        FeedbackBar.Severity = severity;
        FeedbackBar.Title = title;
        FeedbackBar.Message = message;
        FeedbackBar.IsOpen = true;
    }

    private void OnLayoutSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (AccountsLayout is null) return;
        var compact = e.NewSize.Width < 760;
        AccountsLayout.ColumnDefinitions[1].Width = compact ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(CreationPanel, compact ? 0 : 1);
        Grid.SetRow(CreationPanel, compact ? 1 : 0);
    }
}
