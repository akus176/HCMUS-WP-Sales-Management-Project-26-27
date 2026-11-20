using System.Text.Json;
using CoffeeShop.WinUI.Services;

namespace CoffeeShop.WinUI.Tests;

public sealed class MenuPhotoSourceTests
{
    [Fact]
    public void Imported_photos_cover_product_assignments_original_assets_and_the_unknown_item_fallback()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "unsplash-menu-photos.json")));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var photo in manifest.RootElement.EnumerateArray())
        {
            var key = photo.GetProperty("key").GetString()!;
            Assert.True(keys.Add(key), $"Duplicate storage key: {key}");
            foreach (var item in photo.GetProperty("items").EnumerateArray())
                Assert.Equal(key, Path.GetFileName(MenuPhotoSource.ForItem(item.GetString()!, null).AbsolutePath));
        }
        foreach (var original in new[] { "bakery", "banana-bread", "cafe-bar", "cafe-room", "hero-cafe",
            "cappuccino", "chai", "cold-brew", "croissant", "espresso", "flat-white", "iced-coffee", "iced-tea", "latte", "matcha" })
            Assert.Contains(original + ".jpg", keys);
        Assert.Contains(Path.GetFileName(MenuPhotoSource.ForItem("New seasonal drink", null).AbsolutePath), keys);
    }

    [Theory]
    [InlineData("Cafe Latte", "latte")]
    [InlineData("Midterm Matcha Latte", "matcha")]
    [InlineData("CAPPUCCINO", "cappuccino")]
    [InlineData("Café Latte", "latte")]
    [InlineData("Midterm Café Latte", "latte")]
    [InlineData("Cortado", "unsplash-cortado")]
    [InlineData("Espresso Tonic", "unsplash-espresso-tonic")]
    [InlineData("Strawberry Matcha Latte", "unsplash-strawberry-matcha")]
    [InlineData("Salted Cream Cold Brew", "unsplash-cream-cold-brew")]
    [InlineData("Mango Passion Tea", "unsplash-mango-tea")]
    [InlineData("Chocolate Croissant", "unsplash-chocolate-croissant")]
    [InlineData("Basque Cheesecake", "unsplash-basque-cheesecake")]
    [InlineData("Honey Lemon Green Tea", "unsplash-honey-lemon-tea")]
    [InlineData("Salted Caramel Latte", "unsplash-caramel-latte")]
    [InlineData("Cocoa Oat Latte", "unsplash-mocha")]
    [InlineData("Blueberry Muffin", "unsplash-blueberry-muffin")]
    [InlineData("Almond Croissant", "unsplash-almond-croissant")]
    [InlineData("New seasonal drink", "cafe-bar")]
    [InlineData("../../private", "cafe-bar")]
    public void Menu_photos_use_storage_including_seed_aliases_and_unknown_items(string item, string file)
    {
        Assert.Equal($"http://127.0.0.1:9000/menu-images/{file}.jpg",
            MenuPhotoSource.ForItem(item, null).AbsoluteUri);
    }

    [Theory]
    [InlineData("https://photos.example.test:9443/shop/menu")]
    [InlineData("https://photos.example.test:9443/shop/menu/")]
    public void Custom_storage_keeps_its_bucket_path_with_or_without_trailing_slash(string baseUrl)
    {
        Assert.Equal("https://photos.example.test:9443/shop/menu/latte.jpg",
            MenuPhotoSource.ForItem("Cafe Latte", baseUrl).AbsoluteUri);
    }

    [Theory]
    [InlineData("file:///photos/")]
    [InlineData("/menu-images/")]
    [InlineData("https://photos.example.test/menu/?token=secret")]
    [InlineData("https://user:secret@photos.example.test/menu/")]
    [InlineData("https://photos.example.test/menu/#fragment")]
    public void Storage_configuration_rejects_non_http_urls_and_embedded_credentials(string baseUrl)
    {
        Assert.Throws<ArgumentException>(() => MenuPhotoSource.ForItem("Cafe Latte", baseUrl));
    }
}
