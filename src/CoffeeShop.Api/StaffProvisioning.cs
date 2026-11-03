namespace CoffeeShop.Api;

using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres;
using CoffeeShop.Infrastructure.Postgres.Identity;
using Microsoft.EntityFrameworkCore;

public static class StaffProvisioning
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, bool createUser)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        await StaffRoleSeed.ApplyAsync(db);
        if (!createUser) return;
        string Required(string name) => string.IsNullOrWhiteSpace(configuration[$"ProvisionStaff:{name}"])
            ? throw new InvalidOperationException($"Supply ProvisionStaff:{name} through local secret configuration.")
            : configuration[$"ProvisionStaff:{name}"]!;
        var username = Required("Username").Trim();
        var password = Required("Password");
        var email = Required("Email");
        var fullName = Required("FullName");
        var roles = Required("Roles").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        if (username.Length > 50 || email.Length > 255 || fullName.Length > 150 || password.Length is < 12 or > 1024 ||
            roles.Length == 0 || roles.Any(role => !StaffRoleSeed.Bundles.ContainsKey(role)))
            throw new InvalidOperationException("Invalid staff profile, password length, or operational role selection.");
        if (await db.Users.AnyAsync(user => user.Username == username || user.Email == email.ToLowerInvariant()))
            throw new InvalidOperationException("Staff account already exists; provisioning never overwrites an account.");
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var user = new User(EntityId.New(), username, email, "pending", fullName, null, now, now);
        db.Entry(user).Property(u => u.PasswordHash).CurrentValue = StaffIdentityService.HashPassword(user, password);
        db.Users.Add(user);
        foreach (var roleCode in roles)
        {
            var role = await db.Roles.SingleAsync(role => role.Code == roleCode);
            db.UserRoles.Add(new(user.Id, role.Id, now));
        }
        await db.SaveChangesAsync();
    }
}
