using System.Collections.ObjectModel;

namespace CoffeeShop.Application.Common.Results;

public sealed record Error
{
    public Error(
        string code,
        string description,
        ErrorType type,
        IEnumerable<ErrorDetail>? details = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Code = code;
        Description = description;
        Type = type;
        Details = new ReadOnlyCollection<ErrorDetail>((details ?? []).ToArray());
    }

    public string Code { get; }

    public string Description { get; }

    public ErrorType Type { get; }

    public IReadOnlyList<ErrorDetail> Details { get; }

    public static Error Validation(
        string code,
        string description,
        IEnumerable<ErrorDetail>? details = null) =>
        new(code, description, ErrorType.Validation, details);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Conflict(
        string code,
        string description,
        IEnumerable<ErrorDetail>? details = null) =>
        new(code, description, ErrorType.Conflict, details);

    public static Error Authentication(string code, string description) =>
        new(code, description, ErrorType.Authentication);

    public static Error Authorization(string code, string description) =>
        new(code, description, ErrorType.Authorization);

    public static Error BusinessRule(
        string code,
        string description,
        IEnumerable<ErrorDetail>? details = null) =>
        new(code, description, ErrorType.BusinessRule, details);

    public static Error Unexpected(string code, string description) =>
        new(code, description, ErrorType.Unexpected);
}
