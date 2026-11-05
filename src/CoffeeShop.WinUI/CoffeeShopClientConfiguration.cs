using System.Text.Json;
using CoffeeShop.Infrastructure.Postgres;

namespace CoffeeShop.WinUI;

internal sealed record CoffeeShopClientConfiguration(
    string? ConnectionString,
    Uri ApiBaseAddress,
    string? Source,
    string? Error)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    public static string ConfigurationFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CoffeeShop",
        "appsettings.json");

    public static Uri DefaultApiBaseAddress => new("http://localhost:5232/api/v1/");

    public static CoffeeShopClientConfiguration Load()
    {
        var environmentValue = Environment.GetEnvironmentVariable(
            CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return new(environmentValue.Trim(), LoadApiBaseAddress(null), CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable, null);
        }

        var path = ConfigurationFilePath;
        if (!File.Exists(path))
        {
            return new(null, LoadApiBaseAddress(null), null,
                $"Database connection settings were not found. Set {CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable} " +
                $"or create {path}.");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var connectionString = document.RootElement
                .GetProperty("ConnectionStrings")
                .GetProperty("CoffeeShop")
                .GetString();

            var apiBaseAddress = LoadApiBaseAddress(document.RootElement);
            return string.IsNullOrWhiteSpace(connectionString)
                ? new(null, apiBaseAddress, null, $"The CoffeeShop connection string is empty in {path}.")
                : new(connectionString.Trim(), apiBaseAddress, path, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return new(null, LoadApiBaseAddress(null), null,
                $"Could not read the database connection settings in {path}. Check that it contains a valid ConnectionStrings.CoffeeShop value.");
        }
    }

    private static Uri LoadApiBaseAddress(JsonElement? root)
    {
        var value = Environment.GetEnvironmentVariable("COFFEESHOP_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(value) && root is { } document &&
            document.TryGetProperty("Api", out var api) && api.TryGetProperty("BaseUrl", out var baseUrl))
            value = baseUrl.GetString();
        return Uri.TryCreate(value, UriKind.Absolute, out var address) &&
            address.Scheme is "http" or "https" && address.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
            ? address
            : DefaultApiBaseAddress;
    }
}
