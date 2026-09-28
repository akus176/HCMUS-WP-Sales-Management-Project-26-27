using CoffeeShop.Domain.Common;

namespace CoffeeShop.Domain.Tests.Common;

public sealed class AuditMetadataTests
{
    [Fact]
    public void Create_WithUpdateTimestampButNoActor_ThrowsClearValidationError()
    {
        var createdAt = new UtcTimestamp(new DateTimeOffset(2026, 9, 28, 1, 0, 0, TimeSpan.Zero));
        var updatedAt = new UtcTimestamp(new DateTimeOffset(2026, 9, 28, 2, 0, 0, TimeSpan.Zero));
        var createdBy = new EntityId(Guid.NewGuid());

        var exception = Assert.Throws<ArgumentException>(
            () => new AuditMetadata(createdAt, createdBy, updatedAt, null));

        Assert.Contains("updated actor", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithUpdateBeforeCreation_ThrowsClearValidationError()
    {
        var createdAt = new UtcTimestamp(new DateTimeOffset(2026, 9, 28, 2, 0, 0, TimeSpan.Zero));
        var updatedAt = new UtcTimestamp(new DateTimeOffset(2026, 9, 28, 1, 0, 0, TimeSpan.Zero));
        var actor = new EntityId(Guid.NewGuid());

        var exception = Assert.Throws<ArgumentException>(
            () => new AuditMetadata(createdAt, actor, updatedAt, actor));

        Assert.Contains("before", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithUpdateActorButNoTimestamp_ThrowsClearValidationError()
    {
        var createdAt = new UtcTimestamp(new DateTimeOffset(2026, 9, 28, 1, 0, 0, TimeSpan.Zero));
        var actor = new EntityId(Guid.NewGuid());

        var exception = Assert.Throws<ArgumentException>(
            () => new AuditMetadata(createdAt, actor, null, actor));

        Assert.Contains("updated timestamp", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
