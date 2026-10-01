namespace CoffeeShop.Infrastructure.Postgres;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public class CoffeeShopDbContext : DbContext, IUnitOfWork
{
    public CoffeeShopDbContext(DbContextOptions<CoffeeShopDbContext> options)
        : base(options)
    {
    }

    // Identity
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    // Customers
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    public DbSet<LoyaltySettings> LoyaltySettings => Set<LoyaltySettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoffeeShopDbContext).Assembly);
    }
}
