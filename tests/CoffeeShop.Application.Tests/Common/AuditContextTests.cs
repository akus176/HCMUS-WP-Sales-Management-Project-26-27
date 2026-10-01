namespace CoffeeShop.Application.Tests.Common;

using CoffeeShop.Application.Common.Audit;
using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;

public class AuditContextTests
{
    private readonly TestClock _clock;
    private readonly TestCurrentUser _user;
    private readonly AuditContext _auditContext;

    public AuditContextTests()
    {
        _clock = new TestClock(new DateTimeOffset(2026, 3, 15, 8, 30, 0, TimeSpan.Zero));
        _user = TestCurrentUser.Authenticated(EntityId.New(), "barista_bob");
        _auditContext = new AuditContext(_clock, _user);
    }

    [Fact]
    public void Constructor_NullClock_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditContext(null!, _user));
    }

    [Fact]
    public void Constructor_NullCurrentUser_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AuditContext(_clock, null!));
    }

    [Fact]
    public void CreateMetadata_ReturnsAuditMetadataWithCurrentTimestampAndUser()
    {
        var metadata = _auditContext.CreateMetadata();

        Assert.NotNull(metadata);
        Assert.Equal(_clock.UtcNow, metadata.CreatedAt);
        Assert.Equal(_user.Id, metadata.CreatedBy);
        Assert.Null(metadata.UpdatedAt);
        Assert.Null(metadata.UpdatedBy);
    }

    [Fact]
    public void CreateMetadata_WhenUserUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        var unauthenticatedUser = TestCurrentUser.Unauthenticated();
        var context = new AuditContext(_clock, unauthenticatedUser);

        Assert.Throws<UnauthorizedAccessException>(() => context.CreateMetadata());
    }

    [Fact]
    public void TouchUpdated_PreservesCreationMetadataAndSetsUpdatedFields()
    {
        var original = _auditContext.CreateMetadata();

        _clock.Advance(TimeSpan.FromMinutes(30));
        var updatingActorId = EntityId.New();
        _user.Id = updatingActorId;
        _user.Username = "manager_carol";

        var updated = _auditContext.TouchUpdated(original);

        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.Equal(original.CreatedBy, updated.CreatedBy);
        Assert.Equal(_clock.UtcNow, updated.UpdatedAt);
        Assert.Equal(updatingActorId, updated.UpdatedBy);
    }

    [Fact]
    public void TouchUpdated_WhenClockIsBeforeCreatedAt_ThrowsInvalidOperationException()
    {
        var original = _auditContext.CreateMetadata();

        // Rewind clock before created instant
        _clock.Set(original.CreatedAt.Value.AddMinutes(-10));

        var exception = Assert.Throws<InvalidOperationException>(() => _auditContext.TouchUpdated(original));
        Assert.Contains("cannot be earlier than the created timestamp", exception.Message);
    }

    [Fact]
    public void TouchUpdated_NullMetadata_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _auditContext.TouchUpdated(null!));
    }

    [Fact]
    public void TouchUpdated_WhenUserUnauthenticated_ThrowsUnauthorizedAccessException()
    {
        var original = _auditContext.CreateMetadata();

        var unauthenticatedUser = TestCurrentUser.Unauthenticated();
        var context = new AuditContext(_clock, unauthenticatedUser);

        Assert.Throws<UnauthorizedAccessException>(() => context.TouchUpdated(original));
    }
}
