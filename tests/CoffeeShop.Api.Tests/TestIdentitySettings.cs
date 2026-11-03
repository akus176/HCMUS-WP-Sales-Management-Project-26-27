namespace CoffeeShop.Api.Tests;

using Microsoft.AspNetCore.Hosting;

internal static class TestIdentitySettings
{
    // Public test-only key. The running API has no fallback signing key.
    internal const string SigningKey = "test-only-staff-identity-signing-key-150-do-not-deploy";
    public static IWebHostBuilder UseTestIdentity(this IWebHostBuilder builder) => builder
        .UseSetting("Jwt:SigningKey", SigningKey)
        .UseSetting("Jwt:Issuer", "coffeeshop-test")
        .UseSetting("Jwt:Audience", "staff-test");
}
