namespace CoffeeShop.WinUI.Services;

public static class MenuPhotoSource
{
    public const string BaseUrlEnvironmentVariable = "COFFEESHOP_MENU_PHOTOS_BASE_URL";
    public const string DefaultBaseUrl = "http://127.0.0.1:9000/menu-images/";

    private static readonly Dictionary<string, string> Photos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Americano"] = "espresso",
        ["Cappuccino"] = "cappuccino",
        ["Flat White"] = "flat-white",
        ["Cortado"] = "unsplash-cortado",
        ["Espresso Tonic"] = "unsplash-espresso-tonic",
        ["Cold Brew"] = "cold-brew",
        ["Salted Cream Cold Brew"] = "unsplash-cream-cold-brew",
        ["Vietnamese Iced Coffee"] = "iced-coffee",
        ["Matcha Latte"] = "matcha",
        ["Strawberry Matcha Latte"] = "unsplash-strawberry-matcha",
        ["Mango Passion Tea"] = "unsplash-mango-tea",
        ["Honey Lemon Green Tea"] = "unsplash-honey-lemon-tea",
        ["Chai Latte"] = "chai",
        ["Peach Iced Tea"] = "iced-tea",
        ["Butter Croissant"] = "croissant",
        ["Almond Croissant"] = "unsplash-almond-croissant",
        ["Chocolate Croissant"] = "unsplash-chocolate-croissant",
        ["Basque Cheesecake"] = "unsplash-basque-cheesecake",
        ["Banana Bread"] = "banana-bread",
        ["House Espresso"] = "espresso",
        ["Cafe Latte"] = "latte",
        ["Café Latte"] = "latte",
        ["Vanilla Latte"] = "latte",
        ["Caramel Macchiato"] = "unsplash-caramel-latte",
        ["Salted Caramel Latte"] = "unsplash-caramel-latte",
        ["Mocha"] = "unsplash-mocha",
        ["Cocoa Oat Latte"] = "unsplash-mocha",
        ["Coconut Cold Brew"] = "cold-brew",
        ["Peach Green Tea"] = "iced-tea",
        ["Jasmine Milk Tea"] = "chai",
        ["Strawberry Frappe"] = "iced-coffee",
        ["Chocolate Frappe"] = "iced-coffee",
        ["Mango Smoothie"] = "iced-tea",
        ["Blueberry Muffin"] = "unsplash-blueberry-muffin",
        ["Pumpkin Spice Latte"] = "latte",
        ["Honey Oat Latte"] = "latte"
    };

    public static Uri ForItem(string name) => ForItem(name,
        Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable));

    internal static Uri ForItem(string name, string? baseUrl)
    {
        var value = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var address) ||
            address.Scheme is not ("http" or "https") ||
            address.Query.Length != 0 || address.Fragment.Length != 0 || address.UserInfo.Length != 0)
            throw new ArgumentException($"{BaseUrlEnvironmentVariable} must be an HTTP(S) bucket URL.", nameof(baseUrl));

        if (name.StartsWith("Midterm ", StringComparison.OrdinalIgnoreCase)) name = name[8..];
        var file = Photos.TryGetValue(name, out var photo) ? photo : "cafe-bar";
        return new Uri(new Uri(address.AbsoluteUri.TrimEnd('/') + "/"), file + ".jpg");
    }
}
