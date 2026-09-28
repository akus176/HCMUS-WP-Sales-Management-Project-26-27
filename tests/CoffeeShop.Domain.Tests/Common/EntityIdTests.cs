using CoffeeShop.Domain.Common;

namespace CoffeeShop.Domain.Tests.Common;

public sealed class EntityIdTests
{
    [Fact]
    public void Create_WithEmptyGuid_ThrowsClearValidationError()
    {
        var exception = Assert.Throws<ArgumentException>(() => new EntityId(Guid.Empty));

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
