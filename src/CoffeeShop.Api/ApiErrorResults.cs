namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Results;

public static class ApiErrorResults
{
    public static IResult From(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Authentication => StatusCodes.Status401Unauthorized,
            ErrorType.Authorization => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Details.Count > 0)
        {
            extensions["errors"] = error.Details
                .GroupBy(detail => detail.Target)
                .ToDictionary(group => group.Key, group => group.Select(detail => detail.Message).ToArray());
        }
        return Results.Problem(statusCode: status, title: error.Type.ToString(),
            detail: status == 500 ? "An unexpected error occurred." : error.Description,
            extensions: extensions);
    }
}
