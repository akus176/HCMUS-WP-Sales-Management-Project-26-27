using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CoffeeShop.WinUI.Services;

public static class ApiResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        if (response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
                return default!;
            try
            {
                return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken))!;
            }
            catch (JsonException exception)
            {
                throw new ApiException(ApiErrorKind.Unexpected, response.StatusCode, "Invalid server response", "The server returned an unreadable response.", null, exception);
            }
        }

        ProblemPayload? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(JsonOptions, cancellationToken);
        }
        catch (JsonException) { }
        var status = (int)response.StatusCode;
        var kind = status switch
        {
            400 => ApiErrorKind.Validation,
            401 => ApiErrorKind.Unauthorized,
            403 => ApiErrorKind.Forbidden,
            404 => ApiErrorKind.NotFound,
            409 => ApiErrorKind.Conflict,
            422 => ApiErrorKind.BusinessRule,
            >= 500 => ApiErrorKind.Server,
            _ => ApiErrorKind.Unexpected,
        };
        var title = problem?.Title ?? response.ReasonPhrase ?? "Request failed";
        var detail = problem?.Detail ?? "The request could not be completed.";
        throw new ApiException(kind, response.StatusCode, title, detail, problem?.Errors);
    }

    private sealed record ProblemPayload(string? Title, string? Detail, Dictionary<string, string[]>? Errors);
}
