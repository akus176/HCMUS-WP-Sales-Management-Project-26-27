using CoffeeShop.Domain.Inventory;
using Microsoft.UI.Xaml.Controls;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Create/edit dialog for suppliers.
/// </summary>
public sealed partial class SupplierDialog : ContentDialog
{
    private readonly Supplier? editing;

    public SupplierDialog(Supplier? supplier = null)
    {
        InitializeComponent();
        editing = supplier;

        Title = supplier is null ? "New supplier" : "Edit supplier";
        PrimaryButtonText = supplier is null ? "Create" : "Save";
        CloseButtonText = "Cancel";
        DefaultButton = ContentDialogButton.Primary;

        if (supplier is not null)
        {
            NameBox.Text = supplier.Name;
            PhoneBox.Text = supplier.Phone;
            EmailBox.Text = supplier.Email;
            AddressBox.Text = supplier.Address;
        }
    }

    public string SupplierName => NameBox.Text.Trim();

    public string Phone => PhoneBox.Text.Trim();

    public string? Email => string.IsNullOrWhiteSpace(EmailBox.Text) ? null : EmailBox.Text.Trim();

    public string? Address => string.IsNullOrWhiteSpace(AddressBox.Text) ? null : AddressBox.Text.Trim();

    private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(SupplierName))
        {
            args.Cancel = true;
            NameBox.Header = "Name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            args.Cancel = true;
            PhoneBox.Header = "Phone is required.";
            return;
        }
    }
}
