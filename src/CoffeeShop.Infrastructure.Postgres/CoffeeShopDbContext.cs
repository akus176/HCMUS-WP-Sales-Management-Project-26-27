namespace CoffeeShop.Infrastructure.Postgres;

using CoffeeShop.Application.Common.Persistence;
using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Domain.Reporting;
using CoffeeShop.Domain.Sales;
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

    // Identity
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<MobileDeviceRegistration> MobileDeviceRegistrations => Set<MobileDeviceRegistration>();

    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();
    public DbSet<AiInteractionAudit> AiInteractionAudits => Set<AiInteractionAudit>();

    // Customers
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerAddress> CustomerAddresses => Set<CustomerAddress>();

    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    public DbSet<LoyaltySettings> LoyaltySettings => Set<LoyaltySettings>();

    // Inventory
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    public DbSet<PurchaseReceiptLine> PurchaseReceiptLines => Set<PurchaseReceiptLine>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<OrderInventoryRestoration> OrderInventoryRestorations => Set<OrderInventoryRestoration>();

    // Sales and fulfillment
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    public DbSet<OrderLineAddon> OrderLineAddons => Set<OrderLineAddon>();

    public DbSet<Discount> Discounts => Set<Discount>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryStatusHistory> DeliveryStatusHistories => Set<DeliveryStatusHistory>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RecordIdentityChanges();
        var accounts = AccountsNeedingRevocation();
        using var transaction = accounts.Length > 0 && Database.CurrentTransaction is null ? Database.BeginTransaction() : null;
        foreach (var account in accounts)
        {
            Database.ExecuteSqlInterpolated($"SELECT id FROM identity.users WHERE id = {account.Value} FOR UPDATE");
            foreach (var token in RefreshTokens.Where(t => t.UserId == account).ToArray())
                token.Revoke(new CoffeeShop.Domain.Common.UtcTimestamp(DateTimeOffset.UtcNow));
        }
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        transaction?.Commit();
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RecordIdentityChanges();
        var accounts = AccountsNeedingRevocation();
        await using var transaction = accounts.Length > 0 && Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken) : null;
        foreach (var account in accounts)
        {
            await Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM identity.users WHERE id = {account.Value} FOR UPDATE", cancellationToken);
            foreach (var token in await RefreshTokens.Where(t => t.UserId == account).ToArrayAsync(cancellationToken))
                token.Revoke(new CoffeeShop.Domain.Common.UtcTimestamp(DateTimeOffset.UtcNow));
        }
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private CoffeeShop.Domain.Common.EntityId[] AccountsNeedingRevocation() => ChangeTracker.Entries<User>()
        .Where(e => e.State == EntityState.Modified &&
            ((e.Property(u => u.IsActive).IsModified && !e.Entity.IsActive) || e.Property(u => u.PasswordHash).IsModified))
        .Select(e => e.Entity.Id).OrderBy(id => id.Value).ToArray();

    private void RecordIdentityChanges()
    {
        ChangeTracker.DetectChanges();
        var events = new List<SecurityAuditEvent>();
        var now = new CoffeeShop.Domain.Common.UtcTimestamp(DateTimeOffset.UtcNow);
        foreach (var entry in ChangeTracker.Entries<User>().Where(e => e.State == EntityState.Modified).ToArray())
        {
            if (entry.Property(u => u.IsActive).IsModified)
                events.Add(new(CoffeeShop.Domain.Common.EntityId.New(),
                    entry.Entity.IsActive ? "account_reactivated" : "account_deactivated", now, "success", entry.Entity.Id));
        }
        foreach (var entry in ChangeTracker.Entries<UserRole>()
                     .Where(e => e.State is EntityState.Added or EntityState.Deleted).ToArray())
        {
            events.Add(new(CoffeeShop.Domain.Common.EntityId.New(), "role_changed", now, "success", entry.Entity.UserId,
                metadata: System.Text.Json.JsonSerializer.Serialize(new { roleId = entry.Entity.RoleId.Value, action = entry.State.ToString() })));
        }
        SecurityAuditEvents.AddRange(events);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoffeeShopDbContext).Assembly);
    }
}
