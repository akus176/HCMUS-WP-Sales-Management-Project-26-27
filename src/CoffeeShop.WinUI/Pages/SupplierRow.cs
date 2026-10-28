using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Read-model row for the suppliers list.
/// </summary>
public sealed record SupplierRow
{
    public SupplierRow(Supplier supplier, int receiptCount = 0, int postedReceiptCount = 0)
    {
        Id = supplier.Id;
        Name = supplier.Name;
        Phone = supplier.Phone;
        Email = supplier.Email ?? string.Empty;
        Address = supplier.Address ?? string.Empty;
        StatusDisplay = supplier.IsActive ? "Active" : "Inactive";
        ToggleActiveLabel = supplier.IsActive ? "Deactivate" : "Activate";
        IsActive = supplier.IsActive;
        ContactDisplay = string.Join(" · ", new[] { Phone, Email }.Where(value => !string.IsNullOrWhiteSpace(value)));
        AddressDisplay = string.IsNullOrWhiteSpace(Address) ? "No address recorded" : Address;
        ReceiptSummary = receiptCount == 0
            ? "No receipts"
            : $"{receiptCount} receipt{(receiptCount == 1 ? string.Empty : "s")}" +
              (postedReceiptCount > 0 ? $" · {postedReceiptCount} posted" : string.Empty);
        SinceDisplay = $"Since {supplier.CreatedAt.Value.ToLocalTime():d MMM yyyy}";
        HistoryDisplay = $"Added {supplier.CreatedAt.Value.ToLocalTime():d MMM yyyy} · Last updated {supplier.UpdatedAt.Value.ToLocalTime():d MMM yyyy}";
        ToggleActionName = $"{ToggleActiveLabel} supplier {supplier.Name}";
        EditActionName = $"Edit supplier {supplier.Name}";
    }

    public EntityId Id { get; }

    public string Name { get; }

    public string Phone { get; }

    public string Email { get; }

    public string Address { get; }

    public string StatusDisplay { get; }

    public string ToggleActiveLabel { get; }

    public bool IsActive { get; }

    public string ContactDisplay { get; }

    public string AddressDisplay { get; }

    public string ReceiptSummary { get; }

    public string SinceDisplay { get; }

    public string HistoryDisplay { get; }

    public string ToggleActionName { get; }

    public string EditActionName { get; }

    public override string ToString() => $"{Name}, {StatusDisplay}, {ContactDisplay}";
}
