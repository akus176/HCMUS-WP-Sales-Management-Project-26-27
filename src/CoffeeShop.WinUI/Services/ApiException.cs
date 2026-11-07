using System.Net;

namespace CoffeeShop.WinUI.Services;

public enum ApiErrorKind { Validation, Unauthorized, Forbidden, NotFound, Conflict, BusinessRule, Server, Network, Unexpected }

public sealed class ApiException(
    ApiErrorKind kind,
    HttpStatusCode? statusCode,
    string title,
    string detail,
    IReadOnlyDictionary<string, string[]>? errors = null,
    Exception? innerException = null) : Exception(detail, innerException)
{
    public ApiErrorKind Kind { get; } = kind;
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string Detail { get; } = detail;
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}
