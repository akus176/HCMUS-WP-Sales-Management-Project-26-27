namespace CoffeeShop.Infrastructure.Postgres.Identity;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public static class StaffRoleSeed
{
    public static IReadOnlyDictionary<string, string[]> Bundles { get; } = new Dictionary<string, string[]>
    {
        ["Admin"] = ["users.manage", "menu.manage", "tables.manage", "orders.create", "orders.confirm", "orders.cancel", "orders.complete", "orders.fulfill", "discounts.request", "discounts.approve", "payments.record", "inventory.receive", "inventory.adjust", "inventory.manage", "customers.manage", "loyalty.redeem", "delivery.assign", "delivery.update", "reports.revenue", "reports.bestsellers", "reports.channels", "reports.lowstock", "assistant.ask"],
        ["Manager"] = ["menu.manage", "tables.manage", "orders.create", "orders.confirm", "orders.cancel", "orders.complete", "orders.fulfill", "discounts.request", "discounts.approve", "payments.record", "inventory.receive", "inventory.adjust", "inventory.manage", "customers.manage", "loyalty.redeem", "delivery.assign", "reports.revenue", "reports.bestsellers", "reports.channels", "reports.lowstock", "assistant.ask"],
        ["Cashier"] = ["orders.create", "orders.confirm", "orders.complete", "discounts.request", "payments.record", "customers.manage", "loyalty.redeem"],
        ["Barista"] = ["orders.fulfill"],
        ["InventoryStaff"] = ["inventory.receive", "inventory.adjust", "inventory.manage", "reports.lowstock"],
        ["DeliveryStaff"] = ["delivery.update"],
    };

    // Explicit provisioning command, not automatic startup mutation. No accounts or passwords are seeded.
    public static async Task ApplyAsync(CoffeeShopDbContext db, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(150001)", cancellationToken);
        var roles = await db.Roles.ToDictionaryAsync(r => r.Code, cancellationToken);
        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, cancellationToken);
        var links = await db.RolePermissions.ToListAsync(cancellationToken);
        foreach (var (code, bundle) in Bundles)
        {
            if (!roles.TryGetValue(code, out var role))
            {
                role = new Role(EntityId.New(), code, code);
                db.Roles.Add(role);
                roles.Add(code, role);
            }
            foreach (var permissionCode in bundle)
            {
                if (!permissions.TryGetValue(permissionCode, out var permission))
                {
                    permission = new Permission(EntityId.New(), permissionCode);
                    db.Permissions.Add(permission);
                    permissions.Add(permissionCode, permission);
                }
                if (!links.Any(link => link.RoleId == role.Id && link.PermissionId == permission.Id))
                {
                    var link = new RolePermission(role.Id, permission.Id);
                    db.RolePermissions.Add(link);
                    links.Add(link);
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
