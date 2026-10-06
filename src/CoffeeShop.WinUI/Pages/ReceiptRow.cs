using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Read-model row for the receipt list.
/// </summary>
public sealed record ReceiptRow
{
    public ReceiptRow(PurchaseReceipt receipt, string supplierName)
    {
        Id = receipt.Id;
        ReceiptNumber = receipt.ReceiptNumber;
        SupplierName = supplierName;
        StatusDisplay = receipt.Status.ToString();
        LineCount = receipt.Lines.Count.ToString();
        Status = receipt.Status;
    }

    public EntityId Id { get; }

    public string ReceiptNumber { get; }

    public string SupplierName { get; }

    public string StatusDisplay { get; }

    public string LineCount { get; }

    public PurchaseReceiptStatus Status { get; }
}
