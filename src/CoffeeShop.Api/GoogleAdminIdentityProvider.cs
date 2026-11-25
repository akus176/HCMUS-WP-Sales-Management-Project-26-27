namespace CoffeeShop.Api;

using System.Text.Json;
using CoffeeShop.Contracts;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

internal sealed class GoogleAdminIdentityProvider(
    HttpClient client, GoogleAdminOptions options, IConfigurationManager<OpenIdConnectConfiguration> metadata)
{
    public async Task<GoogleAdminLink?> VerifyAsync(GoogleAdminLoginRequest request, CancellationToken ct)
    {
        if (request.Code is not { Length: > 0 and <= 2048 } ||
            request.CodeVerifier is not { Length: >= 43 and <= 128 } verifier ||
            !verifier.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '~') ||
            !Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out var redirect) ||
            redirect.Port is <= 1023 || request.RedirectUri != GoogleAdminOAuth.LoopbackRedirect(redirect.Port))
            return null;

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["code"] = request.Code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = request.RedirectUri,
        });
        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", form, ct);
        if ((int)response.StatusCode is >= 400 and < 500 && (int)response.StatusCode != 429) return null;
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("id_token", out var idToken) ||
            idToken.ValueKind != JsonValueKind.String || idToken.GetString() is not { Length: > 0 and <= 16384 } token)
            return null;

        var keys = await metadata.GetConfigurationAsync(ct);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            IssuerSigningKeys = keys.SigningKeys,
            ValidateIssuerSigningKey = true,
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
            ValidAudience = options.ClientId,
            IgnoreTrailingSlashWhenValidatingAudience = false,
            ClockSkew = TimeSpan.FromMinutes(1),
        });
        if (result.Exception is SecurityTokenSignatureKeyNotFoundException) metadata.RequestRefresh();
        if (!result.IsValid || result.SecurityToken is not JsonWebToken verified ||
            !verified.Audiences.SequenceEqual([options.ClientId]) ||
            (verified.TryGetPayloadValue<string>("azp", out var presenter) && presenter != options.ClientId) ||
            !verified.TryGetPayloadValue<string>("nonce", out var nonce) || nonce != GoogleAdminOAuth.Challenge(verifier) ||
            !verified.TryGetPayloadValue<bool>("email_verified", out var emailVerified) || !emailVerified ||
            !verified.TryGetPayloadValue<string>("email", out var email))
            return null;

        return options.Links.SingleOrDefault(link => string.Equals(link.Email, email, StringComparison.OrdinalIgnoreCase));
    }
}
