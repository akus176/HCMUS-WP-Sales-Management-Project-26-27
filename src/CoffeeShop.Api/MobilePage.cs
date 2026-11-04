namespace CoffeeShop.Api;

using CoffeeShop.Application.Common.Results;
using CoffeeShop.Contracts;

public static class MobilePage
{
    public static IResult From<T>(IReadOnlyList<T> source, int? page, int? pageSize)
    {
        var number = page ?? 1;
        var size = pageSize ?? 20;
        if (number < 1 || size is < 1 or > 200)
            return ApiErrorResults.From(Error.Validation("mobile.invalid_page",
                "Page must be positive and pageSize must be between 1 and 200."));

        var skip = ((long)number - 1) * size;
        T[] items = skip >= source.Count ? [] : source.Skip((int)skip).Take(size).ToArray();
        return Results.Ok(new PageResponse<T>(items, number, size, source.Count));
    }
}
