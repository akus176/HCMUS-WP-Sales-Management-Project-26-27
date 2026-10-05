namespace CoffeeShop.Domain.Catalog;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;

public class RecipeIngredient
{
    protected RecipeIngredient()
    {
    }

    public RecipeIngredient(EntityId id, SizeVariant sizeVariant, Ingredient ingredient, decimal quantity, string unit)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(sizeVariant);
        ArgumentNullException.ThrowIfNull(ingredient);
        if (!ingredient.IsActive)
        {
            throw new ArgumentException("Recipe ingredient must be active.", nameof(ingredient));
        }

        var measuredQuantity = new MeasuredQuantity(quantity, unit);
        // Recipe quantities use numeric(12,3), narrower than inventory quantities.
        if (quantity <= 0 || quantity > 999_999_999.999m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Recipe quantity must be positive and fit numeric(12,3).");
        }

        var baseQuantity = ingredient.ConvertToBaseUnit(measuredQuantity.Value, measuredQuantity.Unit);
        if (baseQuantity is null)
        {
            throw new ArgumentException("Recipe unit must be compatible with the ingredient's base unit.", nameof(unit));
        }

        // Stock uses numeric(14,3); conversion must not overflow or require rounding.
        if (baseQuantity > MeasuredQuantity.MaximumValue || decimal.Round(baseQuantity.Value, 3) != baseQuantity.Value)
        {
            throw new ArgumentException("Recipe quantity must convert exactly to a representable base-unit quantity.", nameof(quantity));
        }

        Id = id;
        SizeVariantId = sizeVariant.Id;
        IngredientId = ingredient.Id;
        Quantity = measuredQuantity.Value;
        Unit = measuredQuantity.Unit;
    }

    public EntityId Id { get; protected set; } = null!;

    public EntityId SizeVariantId { get; protected set; } = null!;

    public EntityId IngredientId { get; protected set; } = null!;

    public decimal Quantity { get; protected set; }

    public string Unit { get; protected set; } = string.Empty;
}
