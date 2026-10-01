namespace CoffeeShop.Application.Tests.Common;

using CoffeeShop.Application.Tests.Testing;
using CoffeeShop.Domain.Common;

public class CurrentUserTests
{
    [Fact]
    public void Unauthenticated_SetsPropertiesCorrectly()
    {
        var user = TestCurrentUser.Unauthenticated();

        Assert.False(user.IsAuthenticated);
        Assert.Null(user.Id);
        Assert.Null(user.Username);
        Assert.Empty(user.Roles);
        Assert.Empty(user.Permissions);
    }

    [Fact]
    public void Unauthenticated_GetRequiredId_ThrowsUnauthorizedAccessException()
    {
        var user = TestCurrentUser.Unauthenticated();

        var exception = Assert.Throws<UnauthorizedAccessException>(() => user.GetRequiredId());
        Assert.Equal("Current user is not authenticated.", exception.Message);
    }

    [Fact]
    public void Authenticated_SetsPropertiesAndReturnsRequiredId()
    {
        var expectedId = EntityId.New();
        var user = TestCurrentUser.Authenticated(
            id: expectedId,
            username: "barista_alice",
            roles: ["Barista"],
            permissions: ["orders.prepare", "orders.view"]);

        Assert.True(user.IsAuthenticated);
        Assert.Equal(expectedId, user.Id);
        Assert.Equal(expectedId, user.GetRequiredId());
        Assert.Equal("barista_alice", user.Username);
        Assert.Contains("Barista", user.Roles);
        Assert.Contains("orders.prepare", user.Permissions);
        Assert.Contains("orders.view", user.Permissions);
    }

    [Fact]
    public void RolesAndPermissions_AreCaseInsensitive()
    {
        var user = TestCurrentUser.Authenticated(
            roles: ["Manager"],
            permissions: ["Reports.View"]);

        Assert.True(user.IsInRole("manager"));
        Assert.True(user.IsInRole("MANAGER"));
        Assert.False(user.IsInRole("Admin"));

        Assert.True(user.HasPermission("reports.view"));
        Assert.True(user.HasPermission("REPORTS.VIEW"));
        Assert.False(user.HasPermission("inventory.manage"));
    }

    [Fact]
    public void HasPermission_And_IsInRole_WhenUnauthenticated_ReturnFalse()
    {
        var user = TestCurrentUser.Unauthenticated();
        user.AddRole("Admin");
        user.AddPermission("system.all");

        Assert.False(user.IsInRole("Admin"));
        Assert.False(user.HasPermission("system.all"));
    }
}
