namespace CoffeeShop.Domain.Inventory;

/// <summary>
/// Derived stock status of an ingredient, computed from its balance,
/// reorder threshold, and activation state.
/// </summary>
public enum StockStatus
{
    /// <summary>
    /// The ingredient is active and its balance is above the reorder threshold.
    /// </summary>
    Ok = 0,

    /// <summary>
    /// The ingredient is deactivated (out of operation), or its balance has
    /// reached or fallen below the reorder threshold.
    /// </summary>
    LowStock = 1,
}
