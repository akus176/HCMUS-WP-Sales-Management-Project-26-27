namespace CoffeeShop.Infrastructure.Postgres;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Inventory;
using Microsoft.EntityFrameworkCore;

public class CoffeeShopDbContext : DbContext, IUnitOfWork
{
    public CoffeeShopDbContext(DbContextOptions<CoffeeShopDbContext> options)
        : base(options)
    {
    }

    // Catalog
    public DbSet<Category> Categories => Set<Category>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<SizeVariant> SizeVariants => Set<SizeVariant>();

    public DbSet<Addon> Addons => Set<Addon>();

    public DbSet<MenuItemAddon> MenuItemAddons => Set<MenuItemAddon>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    // Inventory recipe dependency
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

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
