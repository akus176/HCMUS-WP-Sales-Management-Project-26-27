namespace CoffeeShop.Api;

using CoffeeShop.Contracts;

public static class AssistantEndpoints
{
    public static void MapAssistantEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/assistant/ask", async (AssistantAskRequest request, ManagerAssistantService assistant,
            HttpContext http, CancellationToken cancellationToken) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var result = await assistant.AskAsync(request, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ApiErrorResults.From(result.Error!,
                result.Error!.Code == "assistant.provider_unavailable" ? 503 : null);
        }).RequireAuthorization("assistant.ask").Produces<AssistantAskResponse>()
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(503);
    }
}
