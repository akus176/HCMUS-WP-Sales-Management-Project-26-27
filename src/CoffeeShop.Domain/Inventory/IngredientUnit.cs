namespace CoffeeShop.Domain.Inventory;

/// <summary>
/// Defines the supported measurement unit families and conversion factors
/// between units of the same family. Units from different families are
/// incompatible and can never be converted (grams cannot become milliliters).
/// </summary>
public static class IngredientUnit
{
    public const string Gram = "g";
    public const string Kilogram = "kg";
    public const string Milliliter = "ml";
    public const string Liter = "l";
    public const string Piece = "pcs";

    /// <summary>
    /// Conversion factor to the family's canonical unit.
    /// Canonical units: g (mass), ml (volume), pcs (count).
    /// </summary>
    private static readonly Dictionary<string, decimal> FactorsToCanonical = new(StringComparer.OrdinalIgnoreCase)
    {
        [Gram] = 1m,
        [Kilogram] = 1000m,
        [Milliliter] = 1m,
        [Liter] = 1000m,
        [Piece] = 1m,
    };

    /// <summary>
    /// The canonical unit for each family.
    /// </summary>
    private static readonly Dictionary<string, string> CanonicalUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        [Gram] = Gram,
        [Kilogram] = Gram,
        [Milliliter] = Milliliter,
        [Liter] = Milliliter,
        [Piece] = Piece,
    };

    public static bool IsKnown(string unit) =>
        !string.IsNullOrWhiteSpace(unit) && FactorsToCanonical.ContainsKey(unit.Trim());

    public static string Normalize(string unit) =>
        unit.Trim().ToLowerInvariant();

    /// <summary>
    /// Returns the canonical unit of the family the given unit belongs to,
    /// or null when the unit is unknown.
    /// </summary>
    public static string? FamilyOf(string unit)
    {
        if (!IsKnown(unit))
        {
            return null;
        }

        return CanonicalUnits[Normalize(unit)];
    }

    /// <summary>
    /// Determines whether two units belong to the same family and can be
    /// converted into each other.
    /// </summary>
    public static bool AreCompatible(string source, string target)
    {
        var sourceFamily = FamilyOf(source);
        var targetFamily = FamilyOf(target);

        return sourceFamily is not null
            && targetFamily is not null
            && string.Equals(sourceFamily, targetFamily, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts a quantity from one unit to another within the same family.
    /// Returns null when the units are incompatible or unknown.
    /// </summary>
    public static decimal? Convert(decimal value, string from, string to)
    {
        if (!AreCompatible(from, to))
        {
            return null;
        }

        var fromUnit = Normalize(from);
        var toUnit = Normalize(to);

        var canonicalValue = value * FactorsToCanonical[fromUnit];
        return canonicalValue / FactorsToCanonical[toUnit];
    }
}
