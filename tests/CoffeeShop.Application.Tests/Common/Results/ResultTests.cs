using CoffeeShop.Application.Common.Results;

namespace CoffeeShop.Application.Tests.Common.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_ContainsNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void GenericSuccess_ContainsValueAndNoError()
    {
        var result = Result<string>.Success("coffee");

        Assert.True(result.IsSuccess);
        Assert.Equal("coffee", result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_ContainsError()
    {
        var error = Error.Conflict("order.invalid_state", "The order cannot be changed in its current state.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void GenericFailure_DoesNotExposeValue()
    {
        var error = Error.NotFound("order.not_found", "The order was not found.");
        var result = Result<string>.Failure(error);

        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);

        Assert.Contains("does not contain a value", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Failure_WithoutError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result<string>.Failure(null!));
    }

    [Fact]
    public void GenericSuccess_WithoutValue_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
    }
}
