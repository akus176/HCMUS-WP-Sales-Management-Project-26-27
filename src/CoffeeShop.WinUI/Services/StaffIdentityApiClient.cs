using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoffeeShop.Contracts;

namespace CoffeeShop.WinUI.Services;

public sealed class StaffIdentityApiClient(HttpClient httpClient) : IStaffIdentityApi
{
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
