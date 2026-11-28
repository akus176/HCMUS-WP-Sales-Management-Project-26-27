using CoffeeShop.Contracts;
using CoffeeShop.WinUI.Services;
using CoffeeShop.WinUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace CoffeeShop.WinUI.Pages;

public sealed partial class AccountsPage : Page
{
    private static readonly SolidColorBrush ActiveChipBrush = Brush("ConsoleReadyBrush");
    private static readonly SolidColorBrush ActiveChipEdgeBrush = Brush("ConsoleReadyEdgeBrush");
    private static readonly SolidColorBrush InactiveChipBrush = Brush("ConsolePanelBrush");
    private static readonly SolidColorBrush InactiveChipEdgeBrush = Brush("ConsolePanelLineBrush");
    private static readonly SolidColorBrush RoleBrush = Brush("ConsoleAccentTextBrush");
    private static readonly SolidColorBrush NoRoleBrush = Brush("ConsoleMutedInkBrush");

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

    public sealed record AccountRow(
        string Initials,
        string FullName,
        string Identity,
        string RolesText,
        SolidColorBrush RolesBrush,
        string StateLabel,
        SolidColorBrush StateBrush,
        SolidColorBrush StateEdgeBrush,
        string EditName,
        StaffAccountResponse Account)
    {
        public static AccountRow From(StaffAccountResponse account) => new(
            InitialsOf(account.FullName),
            account.FullName,
            $"{account.Username} · {account.Email}",
            account.Roles.Length == 0 ? "No roles assigned" : string.Join(" · ", account.Roles),
            account.Roles.Length == 0 ? NoRoleBrush : RoleBrush,
            account.IsActive ? "ACTIVE" : "INACTIVE",
            account.IsActive ? ActiveChipBrush : InactiveChipBrush,
            account.IsActive ? ActiveChipEdgeBrush : InactiveChipEdgeBrush,
            $"Edit staff account {account.FullName}",
            account);

        private static string InitialsOf(string fullName)
        {
            var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                0 => "?",
                1 => char.ToUpperInvariant(words[0][0]).ToString(),
                _ => string.Concat(char.ToUpperInvariant(words[0][0]), char.ToUpperInvariant(words[^1][0])),
            };
        }
    }

    private static SolidColorBrush Brush(string key) =>
        (SolidColorBrush)Microsoft.UI.Xaml.Application.Current.Resources[key];

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await RefreshAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter or VirtualKey.Accept)
        {
            e.Handled = true;
            await RefreshAsync();
        }
    }

    private async void OnClearSearch(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        SearchBox.Text = string.Empty;
        await RefreshAsync();
    }

    private void OnNewAccount(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        editingAccount = null;
        EditPanel.Visibility = Visibility.Collapsed;
        CreationPanel.Visibility = Visibility.Visible;
        ValidationText.Text = string.Empty;
        ValidationText.Visibility = Visibility.Collapsed;
        ShowForm();
        FullNameBox.Focus(FocusState.Programmatic);
    }

    private void OnCancelCreate(object sender, RoutedEventArgs e) => HideForm();

    private void ShowForm()
    {
        FormHost.Visibility = Visibility.Visible;
        NewAccountButton.Visibility = Visibility.Collapsed;
        PageScroll.ChangeView(null, 0, null, true);
    }

    private void HideForm()
    {
        FormHost.Visibility = Visibility.Collapsed;
        NewAccountButton.Visibility = Visibility.Visible;
        editingAccount = null;
        EditPanel.Visibility = Visibility.Collapsed;
        CreationPanel.Visibility = Visibility.Visible;
        PageScroll.ChangeView(null, 0, null, true);
        DispatcherQueue.TryEnqueue(() => NewAccountButton.Focus(FocusState.Programmatic));
    }

    private void OnEdit(object sender, RoutedEventArgs e)
    {
        if (busy || (sender as Button)?.Tag is not StaffAccountResponse account) return;
        editingAccount = account;
        EditTitleText.Text = account.FullName;
        EditIdentityText.Text = $"@{account.Username} · {account.Email}";
        EditFullNameBox.Text = account.FullName;
        EditUsernameBox.Text = account.Username;
        EditEmailBox.Text = account.Email;
        EditPhoneBox.Text = account.Phone ?? string.Empty;
        editRoleOptions = StaffAccountForm.RoleOptions.Select(option =>
            new StaffRoleOption(option.Code, option.Label, account.Roles.Contains(option.Code, StringComparer.Ordinal))).ToArray();
        EditRolesList.ItemsSource = editRoleOptions;
        EditValidationText.Text = string.Empty;
        EditValidationText.Visibility = Visibility.Collapsed;
        ApplyAccountStateButton(account);
        CreationPanel.Visibility = Visibility.Collapsed;
        EditPanel.Visibility = Visibility.Visible;
        ShowForm();
        EditFullNameBox.Focus(FocusState.Programmatic);
    }

    private void OnCancelEdit(object sender, RoutedEventArgs e) => HideForm();

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
        if (!ShowValidation(ValidationText, validation, FullNameBox))
        {
            FeedbackBar.IsOpen = false;
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
                HideForm();
                DispatcherQueue.TryEnqueue(() => SearchBox.Focus(FocusState.Programmatic));
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
        if (!ShowValidation(EditValidationText, validation, EditFullNameBox))
        {
            FeedbackBar.IsOpen = false;
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
            EditTitleText.Text = updated.FullName;
            EditIdentityText.Text = $"@{updated.Username} · {updated.Email}";
            ShowFeedback(InfoBarSeverity.Success, "Staff account updated", "The profile and assigned roles were saved.");
            HideForm();
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
            ApplyAccountStateButton(changed);
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

    private void ApplyAccountStateButton(StaffAccountResponse account)
    {
        AccountStateButton.Content = account.IsActive ? "Deactivate account" : "Reactivate account";
        AccountStateButton.Style = (Style)Resources[account.IsActive ? "AccountsDanger" : "AccountsButton"];
        EditStateText.Text = account.IsActive ? "ACTIVE" : "INACTIVE";
        EditStateChip.BorderBrush = account.IsActive ? Brush("ConsoleAccentLightBrush") : Brush("ConsoleOnFieldMutedBrush");
        AutomationProperties.SetName(AccountStateButton,
            account.IsActive ? "Deactivate staff account" : "Reactivate staff account");
    }

    private void ShowAccounts(StaffAccountResponse[] results)
    {
        var rows = results.Select(AccountRow.From).ToArray();
        AccountsList.ItemsSource = rows;
        DirectoryCountText.Text = results.Length >= 100
            ? "100+ ACCOUNTS"
            : $"{results.Length} ACCOUNT{(results.Length == 1 ? string.Empty : "S")}";
        var empty = rows.Length == 0;
        AccountsList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        var hasQuery = SearchBox.Text.Trim().Length > 0;
        ClearSearchButton.Visibility = empty && hasQuery ? Visibility.Visible : Visibility.Collapsed;
        EmptyCreateButton.Visibility = empty && !hasQuery ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
        {
            EmptyTitle.Text = hasQuery ? "No staff accounts match this search." : "No staff accounts yet.";
            EmptyMessage.Text = hasQuery
                ? "Check the spelling, or clear the search to see the full directory."
                : "Use the form to create the first staff account.";
            ListStatus.Text = string.Empty;
            ListStatus.Visibility = Visibility.Collapsed;
            return;
        }
        ListStatus.Visibility = Visibility.Visible;
        ListStatus.Text = results.Length >= 100 ? "Showing the first 100 staff accounts. Search to find others." :
            $"Showing {results.Length} staff account{(results.Length == 1 ? string.Empty : "s")}.";
    }

    /// <summary>Returns true when validation passed; otherwise shows an inline, assertive error and focuses the form.</summary>
    private static bool ShowValidation(TextBlock validation, string? message, Control firstField)
    {
        validation.Text = message ?? string.Empty;
        validation.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        if (message is null) return true;
        firstField.Focus(FocusState.Programmatic);
        return false;
    }

    private void SetBusy(bool value, string? message = null)
    {
        busy = value;
        SearchBox.IsEnabled = RefreshButton.IsEnabled = ClearSearchButton.IsEnabled = !value;
        NewAccountButton.IsEnabled = EmptyCreateButton.IsEnabled = !value;
        CreateButton.IsEnabled = CancelCreateButton.IsEnabled = !value;
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
}
