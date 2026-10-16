namespace CoffeeShop.Application.Reporting;

using CoffeeShop.Application.Common.Results;

internal static class ReportPeriodValidation
{
    public static Result<RevenuePeriod> Create(
        DateOnly firstDate,
        DateOnly lastDateInclusive,
        TimeZoneInfo? businessTimeZone)
    {
        if (businessTimeZone is null)
        {
            return Result<RevenuePeriod>.Failure(Validation(
                "reports.time_zone_required", "A business time zone is required.", "businessTimeZone"));
        }

        if (lastDateInclusive < firstDate)
        {
            return Result<RevenuePeriod>.Failure(Validation(
                "reports.invalid_date_range", "The final report date cannot precede the first date.", "lastDateInclusive"));
        }

        try
        {
            return Result<RevenuePeriod>.Success(
                RevenuePeriod.FromLocalDates(firstDate, lastDateInclusive, businessTimeZone));
        }
        catch (ArgumentException exception)
        {
            return Result<RevenuePeriod>.Failure(Validation(
                "reports.invalid_date_boundary", exception.Message, "firstDate,lastDateInclusive"));
        }
    }

    private static Error Validation(string code, string description, string target) =>
        Error.Validation(code, description, [new ErrorDetail(target, description)]);
}
