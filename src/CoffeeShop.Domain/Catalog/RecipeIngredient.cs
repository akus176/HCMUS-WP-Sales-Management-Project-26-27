namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;

public class RecipeIngredient
{
    protected RecipeIngredient()
    {
    }

    public RecipeIngredient(EntityId id, EntityId sizeVariantId, EntityId ingredientId, decimal quantity, string unit)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(sizeVariantId);
        ArgumentNullException.ThrowIfNull(ingredientId);
        var measuredQuantity = new MeasuredQuantity(quantity, unit);
        // Recipe quantities use numeric(12,3), narrower than inventory quantities.
        if (quantity <= 0 || quantity > 999_999_999.999m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Recipe quantity must be positive and fit numeric(12,3).");
        }

        Id = id;
        SizeVariantId = sizeVariantId;
        IngredientId = ingredientId;
        Quantity = measuredQuantity.Value;
        Unit = measuredQuantity.Unit;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId SizeVariantId { get; protected set; } = null!;

    public EntityId IngredientId { get; protected set; } = null!;

    public decimal Quantity { get; protected set; }

    public string Unit { get; protected set; } = string.Empty;
}
