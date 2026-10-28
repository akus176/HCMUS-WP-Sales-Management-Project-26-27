using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.WinUI.Pages;

namespace CoffeeShop.WinUI.Tests;

public sealed class StockRoomDetailPresentationTests
{
    [Fact]
    public void ReceiptLine_FormatsQuantityUnitCostAndLineTotal()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var receipt = new PurchaseReceipt(EntityId.New(), EntityId.New(), "MIDTERM-RECEIPT-002", now, now);
        var line = receipt.AddLine(EntityId.New(), EntityId.New(), 5000m, new Money(0.03m), now);

        var row = new ReceiptLineRow(line, "Whole milk", "ml");

        Assert.Equal($"{5000m:#,0.###} ml", row.QuantityDisplay);
        Assert.Equal(0.03m.ToString("0.00"), row.UnitCostDisplay);
        Assert.Equal(150m.ToString("0.00"), row.LineTotalDisplay);
        Assert.Equal($"{row.QuantityDisplay} × {row.UnitCostDisplay}", row.DetailDisplay);
    }

    [Fact]
    public void ReceiptLine_WithoutUnit_DoesNotLeaveTrailingSpace()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var receipt = new PurchaseReceipt(EntityId.New(), EntityId.New(), "R-1", now, now);
        var line = receipt.AddLine(EntityId.New(), EntityId.New(), 2.5m, new Money(4m), now);

        var row = new ReceiptLineRow(line, "Pastry");

        Assert.Equal($"{2.5m:#,0.###}", row.QuantityDisplay);
        Assert.Equal(10m.ToString("0.00"), row.LineTotalDisplay);
    }

    [Fact]
    public void SupplierRow_SurfacesContactAddressAndReceiptContext()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var supplier = new Supplier(EntityId.New(), "Midterm Fresh Dairy", "0281234568", now, now,
            email: "Dairy@Example.Test", address: "Thu Duc");

        var row = new SupplierRow(supplier, receiptCount: 3, postedReceiptCount: 2);

        Assert.Equal("0281234568 · dairy@example.test", row.ContactDisplay);
        Assert.Equal("Thu Duc", row.AddressDisplay);
        Assert.Equal("3 receipts · 2 posted", row.ReceiptSummary);
        Assert.Contains($"Since {now.Value.ToLocalTime():d MMM yyyy}", row.SinceDisplay);
        Assert.Contains("Last updated", row.HistoryDisplay);
        Assert.Equal("Active", row.StatusDisplay);
        Assert.Equal("Edit supplier Midterm Fresh Dairy", row.EditActionName);
        Assert.Equal("Midterm Fresh Dairy, Active, 0281234568 · dairy@example.test", row.ToString());
    }

    [Fact]
    public void SupplierRow_UsesReadableFallbacksForMissingContactData()
    {
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var supplier = new Supplier(EntityId.New(), "Midterm Tea and Pantry", "0281234569", now, now, isActive: false);

        var row = new SupplierRow(supplier);

        Assert.Equal("No address recorded", row.AddressDisplay);
        Assert.Equal("No receipts", row.ReceiptSummary);
        Assert.Equal("Inactive", row.StatusDisplay);
        Assert.Equal("Activate", row.ToggleActiveLabel);
    }
}
