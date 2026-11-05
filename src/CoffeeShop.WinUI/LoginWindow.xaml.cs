using CoffeeShop.WinUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace CoffeeShop.WinUI;

public sealed partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(520, 460));
    }

    private async void OnSignIn(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        SignInButton.IsEnabled = false;
        try
        {
            await App.Services.GetRequiredService<StaffSessionManager>()
                .LoginAsync(UsernameBox.Text.Trim(), PasswordBox.Password);
            ((App)Application.Current).ShowMainWindow();
            Close();
        }
        catch (ApiException exception)
        {
            ErrorText.Text = exception.Kind == ApiErrorKind.Unauthorized
                ? "The username or password was not accepted."
                : exception.Kind == ApiErrorKind.Network
                    ? "Cannot reach the coffee shop service. Check the API connection and try again."
                    : exception.Detail;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally { SignInButton.IsEnabled = true; }
    }
}
