using CoffeeShop.Contracts;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class AccountsPage : Page
{
    private readonly ApiStaffAccountsSource accounts;
    private bool busy;
    private StaffAccountResponse? editingAccount;
    private StaffRoleOption[] editRoleOptions = [];

    public AccountsPage()
    {
        if (!App.IsApiMode || !App.Services.GetRequiredService<PosOperatorSession>().HasPermission("users.manage"))
            throw new UnauthorizedAccessException("Accounts requires an administrator session.");
        InitializeComponent();
        accounts = App.Services.GetRequiredService<ApiStaffAccountsSource>();
        Loaded += OnLoaded;
    }

    public sealed record AccountRow(string FullName, string Identity, string RoleAndState, StaffAccountResponse Account)
    {
        public static AccountRow From(StaffAccountResponse account) => new(
            account.FullName, $"{account.Username} · {account.Email}",
            $"{string.Join(", ", account.Roles)} · {(account.IsActive ? "Active" : "Inactive")}", account);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await RefreshAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (busy || (sender as Button)?.Tag is not StaffAccountResponse account) return;
        editingAccount = account;
        EditIdentityText.Text = $"Account ID: {account.Id:D}";
        EditFullNameBox.Text = account.FullName;
        EditUsernameBox.Text = account.Username;
        EditEmailBox.Text = account.Email;
        EditPhoneBox.Text = account.Phone ?? string.Empty;
        editRoleOptions = StaffAccountForm.RoleOptions.Select(option =>
            new StaffRoleOption(option.Code, option.Label, account.Roles.Contains(option.Code, StringComparer.Ordinal))).ToArray();
        EditRolesList.ItemsSource = editRoleOptions;
        EditValidationText.Text = string.Empty;
        EditValidationText.Visibility = Visibility.Collapsed;
        AccountStateButton.Content = account.IsActive ? "DEACTIVATE ACCOUNT" : "REACTIVATE ACCOUNT";
        AutomationProperties.SetName(AccountStateButton,
            account.IsActive ? "Deactivate staff account" : "Reactivate staff account");
        CreationPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Visible;
        EditFullNameBox.Focus(FocusState.Programmatic);
    }

    private void OnCancelEdit(object sender, RoutedEventArgs e)
    {
        editingAccount = null;
        EditPanel.Visibility = Visibility.Collapsed;
        CreationPanel.Visibility = Visibility.Visible;
        AccountsList.Focus(FocusState.Programmatic);
    }

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

    private async void OnSaveEdit(object sender, RoutedEventArgs e)
    {
        if (busy || editingAccount is null) return;
        var request = new UpdateStaffRequest(EditUsernameBox.Text, EditFullNameBox.Text, EditEmailBox.Text,
            EditPhoneBox.Text, editRoleOptions.Where(option => option.IsSelected).Select(option => option.Code).ToArray());
        var validation = StaffAccountForm.Validate(request);
        EditValidationText.Text = validation ?? string.Empty;
        EditValidationText.Visibility = validation is null ? Visibility.Collapsed : Visibility.Visible;
        if (validation is not null)
        {
            ShowFeedback(InfoBarSeverity.Warning, "Check account details", validation);
            return;
        }

        SetBusy(true, "Saving staff account changes…");
        try
        {
            var updated = await accounts.UpdateAsync(editingAccount.Id, request);
            try
            {
                var firstPage = await accounts.SearchAsync(SearchBox.Text);
                ShowAccounts(StaffAccountDirectory.IncludeUpdated(updated, firstPage));
            }
            catch (ApiException)
            {
                ShowAccounts(StaffAccountDirectory.IncludeUpdated(updated, []));
                ListStatus.Text = "Changes were saved. Search again to load the rest of the directory.";
            }
            editingAccount = updated;
            ShowFeedback(InfoBarSeverity.Success, "Staff account updated", "The profile and assigned roles were saved.");
        }
        catch (ApiException exception) { ShowFailure(exception); }
        catch (OperationCanceledException)
        {
            ShowFeedback(InfoBarSeverity.Warning, "Request timed out",
                "Refresh the account before retrying; the server may have saved the changes.");
        }
        finally { SetBusy(false); }
    }

    private async void OnChangeAccountState(object sender, RoutedEventArgs e)
    {
        if (busy || editingAccount is null) return;
        var account = editingAccount;
        var isDeactivation = account.IsActive;
        var dialog = new ContentDialog
        {
            Title = isDeactivation ? "Deactivate staff account?" : "Reactivate staff account?",
            Content = isDeactivation
                ? $"{account.FullName} will lose access and existing sessions will be revoked."
                : $"{account.FullName} will be able to sign in again. Existing sessions will remain revoked.",
            PrimaryButtonText = isDeactivation ? "Deactivate" : "Reactivate",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        SetBusy(true, isDeactivation ? "Deactivating staff account…" : "Reactivating staff account…");
        try
        {
            var changed = isDeactivation
                ? await accounts.DeactivateAsync(account.Id)
                : await accounts.ReactivateAsync(account.Id);
            try
            {
                var firstPage = await accounts.SearchAsync(SearchBox.Text);
                ShowAccounts(StaffAccountDirectory.IncludeUpdated(changed, firstPage));
            }
            catch (ApiException)
            {
                ShowAccounts(StaffAccountDirectory.IncludeUpdated(changed, []));
                ListStatus.Text = "The account state changed. Search again to load the rest of the directory.";
            }
            editingAccount = changed;
            AccountStateButton.Content = changed.IsActive ? "DEACTIVATE ACCOUNT" : "REACTIVATE ACCOUNT";
            AutomationProperties.SetName(AccountStateButton,
                changed.IsActive ? "Deactivate staff account" : "Reactivate staff account");
            ShowFeedback(InfoBarSeverity.Success,
                changed.IsActive ? "Staff account reactivated" : "Staff account deactivated",
                $"{changed.FullName}'s account is now {(changed.IsActive ? "active" : "inactive")}.");
        }
        catch (ApiException exception) { ShowFailure(exception); }
        catch (OperationCanceledException)
        {
            ShowFeedback(InfoBarSeverity.Warning, "Request timed out",
                "Refresh the account before retrying; the server may have changed its state.");
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
        AccountsList.IsEnabled = !value;
        SaveEditButton.IsEnabled = CancelEditButton.IsEnabled = AccountStateButton.IsEnabled = !value;
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
                ShowFeedback(InfoBarSeverity.Error, "Account change blocked", exception.Detail);
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
        Grid.SetColumn(EditPanel, compact ? 0 : 1);
        Grid.SetRow(EditPanel, compact ? 1 : 0);
    }
}
