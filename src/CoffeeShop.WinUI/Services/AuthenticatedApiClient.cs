using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CoffeeShop.WinUI.Services;

public sealed class AuthenticatedApiClient
{
    private readonly HttpClient httpClient;
    private readonly StaffSessionManager sessions;

    public AuthenticatedApiClient(HttpClient httpClient, StaffSessionManager sessions)
    {
        this.httpClient = httpClient;
        this.sessions = sessions;
    }

    public Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Get, path), cancellationToken);

    public Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) }, cancellationToken);

    public Task<TResponse> PutAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) }, cancellationToken);

    public Task<TResponse> DeleteAsync<TResponse>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<TResponse>(() => new HttpRequestMessage(HttpMethod.Delete, path), cancellationToken);

    private async Task<TResponse> SendAsync<TResponse>(Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken)
    {
        var accessToken = await sessions.GetValidAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ApiException(ApiErrorKind.Unauthorized, HttpStatusCode.Unauthorized, "Sign in required", "Your session has expired. Sign in again.");

        using var response = await SendWithTokenAsync(requestFactory, accessToken, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return await ApiResponseParser.ReadJsonAsync<TResponse>(response, cancellationToken);

        var refreshedToken = await sessions.RefreshAfterUnauthorizedAsync(accessToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(refreshedToken))
            throw await ReadApiExceptionAsync(response, cancellationToken);

        using var retryResponse = await SendWithTokenAsync(requestFactory, refreshedToken, cancellationToken);
        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            await sessions.ClearLocalSessionAsync(cancellationToken);
            throw await ReadApiExceptionAsync(retryResponse, cancellationToken);
        }
        return await ApiResponseParser.ReadJsonAsync<TResponse>(retryResponse, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithTokenAsync(
        Func<HttpRequestMessage> requestFactory,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ApiException(ApiErrorKind.Network, null, "Connection problem", "Could not connect to the coffee shop service.", null, exception);
        }
    }

    private static async Task<ApiException> ReadApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await ApiResponseParser.ReadJsonAsync<object>(response, cancellationToken);
        }
        catch (ApiException exception)
        {
            return exception;
        }
        return new ApiException(ApiErrorKind.Unauthorized, HttpStatusCode.Unauthorized, "Sign in required", "Your session has expired. Sign in again.");
    }
}
