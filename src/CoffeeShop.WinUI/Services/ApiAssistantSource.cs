namespace CoffeeShop.WinUI.Services;

using CoffeeShop.Contracts;

public sealed class ApiAssistantSource(AuthenticatedApiClient client)
{
    public Task<AssistantAskResponse> AskAsync(AssistantAskRequest request,
        CancellationToken cancellationToken = default) =>
        client.PostAsync<AssistantAskRequest, AssistantAskResponse>("assistant/ask", request, cancellationToken);
}
