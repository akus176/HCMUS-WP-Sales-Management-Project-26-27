namespace CoffeeShop.Domain.Identity;

using CoffeeShop.Domain.Common;

public class RolePermission
{
    // Parameterless constructor for EF Core
    protected RolePermission()
    {
    }

    public RolePermission(
        EntityId roleId,
        EntityId permissionId)
    {
        ArgumentNullException.ThrowIfNull(roleId);
        ArgumentNullException.ThrowIfNull(permissionId);

        RoleId = roleId;
        PermissionId = permissionId;
    }

    public EntityId RoleId { get; protected set; } = null!;

    public Role? Role { get; protected set; }

    public EntityId PermissionId { get; protected set; } = null!;

    public Permission? Permission { get; protected set; }
}
