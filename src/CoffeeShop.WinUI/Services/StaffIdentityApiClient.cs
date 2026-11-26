using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.Services;

public sealed class StaffIdentityApiClient(HttpClient httpClient) : IStaffIdentityApi
{
    internal Task<GoogleAdminConfigResponse> GetGoogleConfigAsync(CancellationToken cancellationToken)
    {
        // The loopback exception supports the local backend. A remote API must use TLS.
        if (httpClient.BaseAddress is not { } address || (!address.IsLoopback && address.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Google sign-in requires an HTTPS API address.");
        return SendAsync<GoogleAdminConfigResponse>(new HttpRequestMessage(HttpMethod.Get, "auth/google/config"), cancellationToken);
    }

    internal Task<TokenResponse> LoginWithGoogleAsync(GoogleAdminLoginRequest request, CancellationToken cancellationToken) =>
        SendAsync<TokenResponse>(new HttpRequestMessage(HttpMethod.Post, "auth/google")
        {
            Content = JsonContent.Create(request),
        }, cancellationToken);

    public Task<TokenResponse> LoginAsync(string username, string password, CancellationToken cancellationToken = default) =>
        SendAsync<TokenResponse>(new HttpRequestMessage(HttpMethod.Post, "auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(username, password)),
        }, cancellationToken);

    public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        SendAsync<TokenResponse>(new HttpRequestMessage(HttpMethod.Post, "auth/refresh")
        {
            Content = JsonContent.Create(new RefreshRequest(refreshToken)),
        }, cancellationToken);

    public async Task LogoutAsync(string refreshToken, string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout")
        {
            Content = JsonContent.Create(new RefreshRequest(refreshToken)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await SendRequestAsync(request, cancellationToken);
        await ApiResponseParser.ReadJsonAsync<object>(response, cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await SendRequestAsync(request, cancellationToken))
            return await ApiResponseParser.ReadJsonAsync<T>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ApiException(ApiErrorKind.Network, null, "Connection problem", "Could not connect to the coffee shop service.", null, exception);
        }
    }
}
