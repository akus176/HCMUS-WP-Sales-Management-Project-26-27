using CoffeeShop.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace CoffeeShop.WinUI;

public sealed partial class LoginWindow : Window
{
    private readonly CancellationTokenSource lifetime = new();
    private bool initialized;
    private bool signingIn;
    private bool closed;

    public LoginWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            closed = true;
            lifetime.Cancel();
            lifetime.Dispose();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (initialized) return;
        initialized = true;
        var scale = LoginRoot.XamlRoot.RasterizationScale;
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id,
            Microsoft.UI.Windowing.DisplayAreaFallback.Nearest).WorkArea;
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
            Math.Min((int)(560 * scale), Math.Max(1, area.Width - 48)),
            Math.Min((int)(720 * scale), Math.Max(1, area.Height - 80))));
        AppWindow.Move(new Windows.Graphics.PointInt32(
            area.X + Math.Max(0, (area.Width - AppWindow.Size.Width) / 2),
            area.Y + Math.Max(0, (area.Height - AppWindow.Size.Height) / 2)));
        UsernameBox.Focus(FocusState.Keyboard);
    }

    private async void OnSignIn(object sender, RoutedEventArgs e) => await SignInAsync();

    private async void OnCredentialsKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        await SignInAsync();
    }

    private void OnUsernameChanged(object sender, TextChangedEventArgs e)
    {
        if (UsernameError is null || string.IsNullOrWhiteSpace(UsernameBox.Text)) return;
        UsernameError.Visibility = Visibility.Collapsed;
        AutomationProperties.SetHelpText(UsernameBox, "");
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordError is null || PasswordBox.Password.Length == 0) return;
        PasswordError.Visibility = Visibility.Collapsed;
        AutomationProperties.SetHelpText(PasswordBox, "");
    }

    private async Task SignInAsync()
    {
        if (signingIn || closed) return;
        SignInFeedback.IsOpen = false;
        var missingUsername = string.IsNullOrWhiteSpace(UsernameBox.Text);
        var missingPassword = PasswordBox.Password.Length == 0;
        UsernameError.Visibility = missingUsername ? Visibility.Visible : Visibility.Collapsed;
        PasswordError.Visibility = missingPassword ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetHelpText(UsernameBox, missingUsername ? UsernameError.Text : "");
        AutomationProperties.SetHelpText(PasswordBox, missingPassword ? PasswordError.Text : "");
        if (missingUsername || missingPassword)
        {
            (missingUsername ? (Control)UsernameBox : PasswordBox).Focus(FocusState.Keyboard);
            return;
        }

        SetSigningIn(true);
        Control retryFocus = SignInButton;
        try
        {
            await App.Services.GetRequiredService<StaffSessionManager>()
                .LoginAsync(UsernameBox.Text.Trim(), PasswordBox.Password, lifetime.Token);
            if (closed) return;
            ((App)Microsoft.UI.Xaml.Application.Current).ShowMainWindow();
            Close();
        }
        catch (ApiException exception)
        {
            if (closed) return;
            var unauthorized = exception.Kind == ApiErrorKind.Unauthorized;
            ShowError(unauthorized ? "Check your sign-in details" : "Couldn't sign in",
                unauthorized ? "The username or password wasn't accepted. Check both and try again."
                : exception.Kind == ApiErrorKind.Network ? "Can't reach Corner Cafe. Check your connection and try again."
                : exception.Kind == ApiErrorKind.Forbidden ? "This account can't access the workspace. Contact your manager for help."
                : "The service couldn't sign you in. Try again, or contact your manager if this continues.");
            if (unauthorized) retryFocus = PasswordBox;
        }
        catch (OperationCanceledException)
        {
            if (!closed) ShowError("Sign-in timed out", "The service took too long to respond. Try again.");
        }
        catch (HttpRequestException)
        {
            if (!closed) ShowError("Couldn't connect", "Can't reach Corner Cafe. Check your connection and try again.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!closed) ShowError("Couldn't finish signing in", "Your session couldn't be saved on this device. Try again, or contact your manager for help.");
        }
        finally
        {
            if (!closed)
            {
                SetSigningIn(false);
                retryFocus.Focus(FocusState.Keyboard);
            }
        }
    }

    private void SetSigningIn(bool value)
    {
        signingIn = value;
        UsernameBox.IsEnabled = PasswordBox.IsEnabled = SignInButton.IsEnabled = !value;
        SignInButton.Content = value ? "Signing in…" : "Sign in";
        SigningInProgress.IsActive = value;
        SigningInProgress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        SigningInStatus.Text = value ? "Checking your account…" : "";
    }

    private void ShowError(string title, string message)
    {
        SignInFeedback.Title = title;
        SignInFeedback.Message = message;
        SignInFeedback.IsOpen = true;
    }
}
