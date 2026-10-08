using System.Globalization;

namespace CoffeeShop.WinUI.ViewModels;

public static class CatalogNumberInput
{
    // Do not accept grouping separators: a mistyped decimal must not become 1000x the price.
    public static bool TryParse(string text, out decimal value) => decimal.TryParse(text,
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite,
        CultureInfo.CurrentCulture, out value);

    public static decimal? Price(string text) => TryParse(text, out var value) ? value : null;
}
