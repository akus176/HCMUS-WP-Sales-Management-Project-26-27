namespace CoffeeShop.Domain.Identity;

using CoffeeShop.Domain.Common;

public class Permission
{
    private readonly List<RolePermission> _rolePermissions = [];

    // Parameterless constructor for EF Core
    protected Permission()
    {
    }

    public Permission(
        EntityId id,
        string code,
        string? description = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Id = id;
        Code = code.Trim();
        Description = description?.Trim();
    }

    public EntityId Id { get; protected set; } = null!;

    public string Code { get; protected set; } = string.Empty;

    public string? Description { get; protected set; }

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();
}
