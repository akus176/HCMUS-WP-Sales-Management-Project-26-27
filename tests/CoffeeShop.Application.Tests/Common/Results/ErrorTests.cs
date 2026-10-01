using CoffeeShop.Application.Common.Results;

namespace CoffeeShop.Application.Tests.Common.Results;

public sealed class ErrorTests
{
    public static TheoryData<Error, ErrorType> ErrorMappings => new()
    {
        { Error.Validation("validation.failed", "Validation failed."), ErrorType.Validation },
        { Error.NotFound("resource.not_found", "The resource was not found."), ErrorType.NotFound },
        { Error.Conflict("resource.conflict", "The resource is in conflict."), ErrorType.Conflict },
        { Error.Authentication("authentication.required", "Authentication is required."), ErrorType.Authentication },
        { Error.Authorization("authorization.denied", "Access is denied."), ErrorType.Authorization },
        { Error.BusinessRule("inventory.insufficient", "Inventory is insufficient."), ErrorType.BusinessRule },
        { Error.Unexpected("unexpected.failure", "An unexpected failure occurred."), ErrorType.Unexpected },
    };

    [Theory]
    [MemberData(nameof(ErrorMappings))]
    public void Factory_MapsToStableErrorType(Error error, ErrorType expectedType)
    {
        Assert.Equal(expectedType, error.Type);
    }

    [Fact]
    public void Validation_WithDetails_PreservesFieldInformation()
    {
        var details = new[]
        {
            new ErrorDetail("quantity", "Quantity must be greater than zero."),
            new ErrorDetail("unit", "Unit is incompatible with the ingredient."),
        };

        var error = Error.Validation("recipe.invalid", "The recipe is invalid.", details);

        Assert.Equal("recipe.invalid", error.Code);
        Assert.Equal("The recipe is invalid.", error.Description);
        Assert.Equal(details, error.Details);
    }

    [Fact]
    public void Create_CopiesDetailsToReadOnlyCollection()
    {
        var details = new List<ErrorDetail>
        {
            new("quantity", "Quantity must be greater than zero."),
        };
        var error = Error.Validation("recipe.invalid", "The recipe is invalid.", details);

        details.Add(new ErrorDetail("unit", "Unit is incompatible with the ingredient."));

        Assert.Single(error.Details);
        Assert.IsAssignableFrom<IReadOnlyList<ErrorDetail>>(error.Details);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutStableCode_Throws(string? code)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => new Error(code!, "A useful description.", ErrorType.Validation));
    }
}
