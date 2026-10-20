using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.WinUI.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public async Task ConnectToDatabaseAsync()
    {
        Nav.IsEnabled = false;
        RetryDatabaseButton.Visibility = Visibility.Collapsed;
        DatabaseStatusText.Text = "Checking database connection...";

        var configuration = CoffeeShopClientConfiguration.Load();
        if (!configuration.IsConfigured)
        {
            ShowDatabaseFailure(configuration.Error ?? "Database connection settings are missing.");
            return;
        }

        try
        {
            App.ConfigureServices(configuration.ConnectionString!);
            using var scope = App.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
            if (!await database.Database.CanConnectAsync())
            {
                ShowDatabaseFailure("PostgreSQL did not accept the connection. Confirm the server is running and the configured database credentials are correct.");
                return;
            }

            DatabaseStatusText.Text = $"Connected to PostgreSQL ({configuration.Source}). The WinUI client is running in direct database mode.";
            Nav.IsEnabled = true;
            Nav.SelectedItem ??= Nav.MenuItems[0];
        }
        catch (Exception)
        {
            ShowDatabaseFailure("Could not connect to PostgreSQL. Confirm the server is running and the configured database name and credentials are correct.");
        }
    }

    private void ShowDatabaseFailure(string message)
    {
        DatabaseStatusText.Text = message;
        RetryDatabaseButton.Visibility = Visibility.Visible;
    }

    private async void OnRetryDatabaseClick(object sender, RoutedEventArgs e)
    {
        await ConnectToDatabaseAsync();
    }

    public void NavigateTo(string tag) => Nav.SelectedItem = Nav.MenuItems.OfType<NavigationViewItem>()
        .Single(item => item.Tag is string itemTag && itemTag == tag);

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
        {
            return;
        }

        ContentFrame.Navigate(tag switch
        {
            "pos" => typeof(PosPage),
            "operations" => typeof(OperationsPage),
            "catalog" => typeof(CatalogPage),
            "customers" => typeof(CustomersPage),
            "ingredients" => typeof(IngredientsPage),
            "suppliers" => typeof(SuppliersPage),
            "receipts" => typeof(PurchaseReceiptsPage),
            "lowstock" => typeof(LowStockPage),
            "reports" => typeof(ReportsPage),
            _ => typeof(IngredientsPage),
        });
    }
}
