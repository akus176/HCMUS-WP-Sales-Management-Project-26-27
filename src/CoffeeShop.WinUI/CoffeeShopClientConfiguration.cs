using System.Text.Json;
using CoffeeShop.Infrastructure.Postgres;

namespace CoffeeShop.WinUI;

internal sealed record CoffeeShopClientConfiguration(
    string? ConnectionString,
    string? Source,
    string? Error)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

    public static string ConfigurationFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CoffeeShop",
        "appsettings.json");

    public static CoffeeShopClientConfiguration Load()
    {
        var environmentValue = Environment.GetEnvironmentVariable(
            CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return new(environmentValue.Trim(), CoffeeShopPostgresOptions.ConnectionStringEnvironmentVariable, null);
        }

        var path = ConfigurationFilePath;
        if (!File.Exists(path))
        {
            return new(null, null,
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

            return string.IsNullOrWhiteSpace(connectionString)
                ? new(null, null, $"The CoffeeShop connection string is empty in {path}.")
                : new(connectionString.Trim(), path, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return new(null, null,
                $"Could not read the database connection settings in {path}. Check that it contains a valid ConnectionStrings.CoffeeShop value.");
        }
    }
}
