namespace CoffeeShop.Domain.Inventory;

public enum StockMovementType
{
    Receipt = 0,
    Adjustment = 1,
    OrderConsumption = 2,
    Restoration = 3,
}