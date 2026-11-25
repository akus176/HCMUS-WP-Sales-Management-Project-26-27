using System.ComponentModel;
using System.Diagnostics;
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
    private CancellationTokenSource? googleCancellation;

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

    private async void OnGoogleSignIn(object sender, RoutedEventArgs e) => await SignInAsync(google: true);

    private void OnCancelGoogleSignIn(object sender, RoutedEventArgs e) => googleCancellation?.Cancel();

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

    private bool ValidateCredentials()
    {
        var missingUsername = string.IsNullOrWhiteSpace(UsernameBox.Text);
        var missingPassword = PasswordBox.Password.Length == 0;
        UsernameError.Visibility = missingUsername ? Visibility.Visible : Visibility.Collapsed;
        PasswordError.Visibility = missingPassword ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetHelpText(UsernameBox, missingUsername ? UsernameError.Text : "");
        AutomationProperties.SetHelpText(PasswordBox, missingPassword ? PasswordError.Text : "");
        if (missingUsername || missingPassword)
        {
            (missingUsername ? (Control)UsernameBox : PasswordBox).Focus(FocusState.Keyboard);
            return false;
        }
        return true;
    }

    private async Task SignInAsync(bool google = false)
    {
        if (signingIn || closed) return;
        SignInFeedback.IsOpen = false;
        if (!google && !ValidateCredentials()) return;

        using var cancellation = google ? CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token) : null;
        googleCancellation = cancellation;
        SetSigningIn(true, google);
        Control retryFocus = google ? GoogleSignInButton : SignInButton;
        try
        {
            if (google)
            {
                CancelGoogleSignInButton.Focus(FocusState.Keyboard);
                var session = await App.Services.GetRequiredService<GoogleAdminSignIn>().LoginAsync(uri =>
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true })?.Dispose(), cancellation!.Token);
                if (session is null) return;
            }
            else
            {
                await App.Services.GetRequiredService<StaffSessionManager>()
                    .LoginAsync(UsernameBox.Text.Trim(), PasswordBox.Password, lifetime.Token);
            }
            if (closed) return;
            ((App)Microsoft.UI.Xaml.Application.Current).ShowMainWindow();
            Close();
        }
        catch (ApiException exception)
        {
            if (closed) return;
            var unauthorized = exception.Kind == ApiErrorKind.Unauthorized;
            if (google)
            {
                ShowError("Couldn't sign in with Google", unauthorized
                    ? "Use the Google account linked to your active administrator profile."
                    : exception.Kind == ApiErrorKind.Network
                        ? "Can't reach Corner Cafe. Check your connection and try again."
                        : "Google sign-in is unavailable. Try again or contact your administrator.");
                return;
            }
            ShowError(unauthorized ? "Check your sign-in details" : "Couldn't sign in",
                unauthorized ? "The username or password wasn't accepted. Check both and try again."
                : exception.Kind == ApiErrorKind.Network ? "Can't reach Corner Cafe. Check your connection and try again."
                : exception.Kind == ApiErrorKind.Forbidden ? "This account can't access the workspace. Contact your manager for help."
                : "The service couldn't sign you in. Try again, or contact your manager if this continues.");
            if (unauthorized) retryFocus = PasswordBox;
        }
        catch (OperationCanceledException)
        {
            if (!closed && cancellation?.IsCancellationRequested != true)
                ShowError("Sign-in timed out", google ? "Finish signing in in your browser and try again." : "The service took too long to respond. Try again.");
        }
        catch (HttpRequestException)
        {
            if (!closed) ShowError("Couldn't connect", "Can't reach Corner Cafe. Check your connection and try again.");
        }
        catch (Exception exception) when (google && exception is Win32Exception or InvalidOperationException)
        {
            if (!closed) ShowError("Couldn't open Google sign-in", "Check that you have a default browser and that Corner Cafe uses a secure connection.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (!closed) ShowError("Couldn't finish signing in", "Your session couldn't be saved on this device. Try again, or contact your manager for help.");
        }
        finally
        {
            googleCancellation = null;
            if (!closed)
            {
                SetSigningIn(false);
                retryFocus.Focus(FocusState.Keyboard);
            }
        }
    }

    private void SetSigningIn(bool value, bool google = false)
    {
        signingIn = value;
        UsernameBox.IsEnabled = PasswordBox.IsEnabled = SignInButton.IsEnabled = GoogleSignInButton.IsEnabled = !value;
        SignInButton.Content = value && !google ? "Signing in…" : "Sign in";
        CancelGoogleSignInButton.Visibility = value && google ? Visibility.Visible : Visibility.Collapsed;
        SigningInProgress.IsActive = value;
        SigningInProgress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        SigningInStatus.Text = !value ? "" : google ? "Continue in your browser…" : "Checking your account…";
    }

    private void ShowError(string title, string message)
    {
        SignInFeedback.Title = title;
        SignInFeedback.Message = message;
        SignInFeedback.IsOpen = true;
    }
}
