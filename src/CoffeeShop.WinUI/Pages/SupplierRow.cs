using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

namespace CoffeeShop.WinUI.Pages;

/// <summary>
/// Read-model row for the suppliers list.
/// </summary>
public sealed record SupplierRow
{
    public SupplierRow(Supplier supplier)
    {
        Id = supplier.Id;
        Name = supplier.Name;
        Phone = supplier.Phone;
        Email = supplier.Email ?? string.Empty;
        StatusDisplay = supplier.IsActive ? "Active" : "Inactive";
        ToggleActiveLabel = supplier.IsActive ? "Deactivate" : "Activate";
        IsActive = supplier.IsActive;
    }

    public EntityId Id { get; }

    public string Name { get; }

    public string Phone { get; }

    public string Email { get; }

    public string StatusDisplay { get; }

    public string ToggleActiveLabel { get; }

    public bool IsActive { get; }
}
