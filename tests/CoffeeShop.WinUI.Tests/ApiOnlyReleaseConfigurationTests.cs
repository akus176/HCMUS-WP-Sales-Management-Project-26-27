namespace CoffeeShop.WinUI.Tests;

using CoffeeShop.WinUI;

public sealed class ApiOnlyReleaseConfigurationTests
{
    [Fact]
    public void Load_UsesOnlyApiSettingsEvenWhenLegacyDirectDatabaseSettingsExist()
    {
        var configuredValues = new Dictionary<string, string?>
        {
            ["COFFEESHOP_CLIENT_MODE"] = "direct",
            ["ConnectionStrings__CoffeeShop"] = "Host=must-not-be-read;Username=sample",
            ["COFFEESHOP_API_BASE_URL"] = "https://api.example.test/api/v1/"
        };
        var readNames = new List<string>();

        var configuration = CoffeeShopClientConfiguration.LoadApiOnly(name =>
        {
            readNames.Add(name);
            return configuredValues.GetValueOrDefault(name);
        });

        Assert.True(configuration.IsApiMode);
        Assert.True(configuration.IsConfigured);
        Assert.Null(configuration.ConnectionString);
        Assert.Equal(new Uri("https://api.example.test/api/v1/"), configuration.ApiBaseAddress);
        Assert.Null(configuration.Error);
        Assert.Equal(["COFFEESHOP_API_BASE_URL"], readNames);
    }
}
