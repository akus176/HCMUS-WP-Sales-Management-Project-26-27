namespace CoffeeShop.WinUI.Services;

using CoffeeShop.Contracts;

public sealed class ApiAssistantSource : IDisposable
{
    private readonly HttpClient http;
    private readonly AuthenticatedApiClient client;

    public ApiAssistantSource(Uri apiBaseAddress, StaffSessionManager sessions)
    {
        http = new HttpClient { BaseAddress = apiBaseAddress, Timeout = TimeSpan.FromSeconds(45) };
        client = new AuthenticatedApiClient(http, sessions);
    }

    public Task<AssistantAskResponse> AskAsync(AssistantAskRequest request,
        CancellationToken cancellationToken = default) =>
        client.PostAsync<AssistantAskRequest, AssistantAskResponse>("assistant/ask", request, cancellationToken);

    public void Dispose() => http.Dispose();
}
