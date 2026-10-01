namespace CoffeeShop.Application.Tests.Testing;

using CoffeeShop.Application.Common.Security;
using CoffeeShop.Domain.Common;

public sealed class TestCurrentUser : ICurrentUser
{
    private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);

    public EntityId? Id { get; set; }

    public string? Username { get; set; }

    public bool IsAuthenticated { get; set; }

    public IReadOnlyCollection<string> Roles => _roles;

    public IReadOnlyCollection<string> Permissions => _permissions;

    public static TestCurrentUser Authenticated(
        EntityId? id = null,
        string username = "testuser",
        IEnumerable<string>? roles = null,
        IEnumerable<string>? permissions = null)
    {
        var user = new TestCurrentUser
        {
            Id = id ?? EntityId.New(),
            Username = username,
            IsAuthenticated = true
        };

        if (roles is not null)
        {
            user.AddRoles(roles);
        }

        if (permissions is not null)
        {
            user.AddPermissions(permissions);
        }

        return user;
    }

    public static TestCurrentUser Unauthenticated() => new()
    {
        IsAuthenticated = false,
        Id = null,
        Username = null
    };

    public void AddRole(string role)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        _roles.Add(role);
    }

    public void AddRoles(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        foreach (var role in roles)
        {
            AddRole(role);
        }
    }

    public void AddPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _permissions.Add(permission);
    }

    public void AddPermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        foreach (var permission in permissions)
        {
            AddPermission(permission);
        }
    }

    public bool HasPermission(string permission) =>
        IsAuthenticated && !string.IsNullOrWhiteSpace(permission) && _permissions.Contains(permission);

    public bool IsInRole(string role) =>
        IsAuthenticated && !string.IsNullOrWhiteSpace(role) && _roles.Contains(role);

    public EntityId GetRequiredId()
    {
        if (!IsAuthenticated || Id is null)
        {
            throw new UnauthorizedAccessException("Current user is not authenticated.");
        }

        return Id;
    }
}
