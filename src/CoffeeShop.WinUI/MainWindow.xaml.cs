using CoffeeShop.WinUI.Pages;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Nav.SelectedItem = Nav.MenuItems[0];
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
            _ => typeof(IngredientsPage),
        });
    }
}
