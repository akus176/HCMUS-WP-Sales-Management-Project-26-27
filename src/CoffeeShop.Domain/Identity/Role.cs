namespace CoffeeShop.Domain.Identity;

using CoffeeShop.Domain.Common;

public class Role
{
    private readonly List<UserRole> _userRoles = [];
    private readonly List<RolePermission> _rolePermissions = [];

    // Parameterless constructor for EF Core
    protected Role()
    {
    }

    public Role(
        EntityId id,
        string code,
        string name,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Code = code.Trim();
        Name = name.Trim();
        Description = description?.Trim();
    }

    public EntityId Id { get; protected set; } = null!;

    public string Code { get; protected set; } = string.Empty;

    public string Name { get; protected set; } = string.Empty;

    public string? Description { get; protected set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();
}
