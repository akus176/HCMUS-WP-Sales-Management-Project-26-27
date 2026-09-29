namespace CoffeeShop.Domain.Identity;

using CoffeeShop.Domain.Common;

public class UserRole
{
    // Parameterless constructor for EF Core
    protected UserRole()
    {
    }

    public UserRole(
        EntityId userId,
        EntityId roleId,
        UtcTimestamp assignedAt,
        EntityId? assignedBy = null)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(roleId);
        ArgumentNullException.ThrowIfNull(assignedAt);

        UserId = userId;
        RoleId = roleId;
        AssignedAt = assignedAt;
        AssignedBy = assignedBy;
    }

    public EntityId UserId { get; protected set; } = null!;

    public User? User { get; protected set; }

    public EntityId RoleId { get; protected set; } = null!;

    public Role? Role { get; protected set; }

    public UtcTimestamp AssignedAt { get; protected set; } = null!;

    public EntityId? AssignedBy { get; protected set; }
}
