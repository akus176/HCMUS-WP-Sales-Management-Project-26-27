namespace CoffeeShop.Domain.Tests.Identity;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;

public class IdentityTests
{
    private static UtcTimestamp Now => new(DateTimeOffset.UtcNow);

    [Fact]
    public void User_WithValidData_ShouldInitializeCorrectly()
    {
        var id = EntityId.New();
        var user = new User(id, "admin", "admin@coffeeshop.internal", "hashed_password", "Admin User", "+1234567890", Now, Now);

        Assert.Equal(id, user.Id);
        Assert.Equal("admin", user.Username);
        Assert.Equal("admin@coffeeshop.internal", user.Email);
        Assert.Equal("hashed_password", user.PasswordHash);
        Assert.Equal("Admin User", user.FullName);
        Assert.True(user.IsActive);
        Assert.Null(user.DeactivatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void User_WithInvalidUsernameOrEmail_ShouldThrow(string invalidInput)
    {
        var id = EntityId.New();
        Assert.Throws<ArgumentException>(() => new User(id, invalidInput, "admin@coffeeshop.internal", "hash", "Admin", null, Now, Now));
        Assert.Throws<ArgumentException>(() => new User(id, "admin", invalidInput, "hash", "Admin", null, Now, Now));
    }

    [Fact]
    public void RefreshToken_WithValidData_ShouldTrackActiveStatus()
    {
        var id = EntityId.New();
        var userId = EntityId.New();
        var familyId = Guid.NewGuid();
        var expiresAt = new UtcTimestamp(DateTimeOffset.UtcNow.AddDays(7));

        var token = new RefreshToken(id, userId, "token_hash_123", familyId, Now, expiresAt);

        Assert.True(token.IsActive);
    }

    [Fact]
    public void RefreshToken_WithEmptyFamilyId_ShouldThrow()
    {
        var id = EntityId.New();
        var userId = EntityId.New();
        var expiresAt = new UtcTimestamp(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Throws<ArgumentException>(() =>
            new RefreshToken(id, userId, "token_hash", Guid.Empty, Now, expiresAt));
    }

    [Fact]
    public void SecurityAuditEvent_ShouldInitializeCorrectly()
    {
        var id = EntityId.New();
        var userId = EntityId.New();

        var evt = new SecurityAuditEvent(id, "login_success", Now, "succeeded", userId, "127.0.0.1", "{\"client\":\"winui\"}");

        Assert.Equal("login_success", evt.EventType);
        Assert.Equal("succeeded", evt.Outcome);
        Assert.Equal("127.0.0.1", evt.IpAddress);
        Assert.Equal("{\"client\":\"winui\"}", evt.Metadata);
    }
}
