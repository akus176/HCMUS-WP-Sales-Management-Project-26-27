namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Domain.Customers;
using CoffeeShop.Domain.Identity;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

public class ModelMappingTests
{
    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<CoffeeShopDbContext>()
            .UseNpgsql("Host=localhost;Database=coffeeshop_test")
            .Options;

        using var context = new CoffeeShopDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void ModelBuilder_ShouldConstructModelWithoutErrors()
    {
        var model = CreateModel();
        Assert.NotNull(model);
    }

    [Theory]
    [InlineData(typeof(User), "users", "identity")]
    [InlineData(typeof(Role), "roles", "identity")]
    [InlineData(typeof(Permission), "permissions", "identity")]
    [InlineData(typeof(UserRole), "user_roles", "identity")]
    [InlineData(typeof(RolePermission), "role_permissions", "identity")]
    [InlineData(typeof(RefreshToken), "refresh_tokens", "identity")]
    [InlineData(typeof(SecurityAuditEvent), "security_audit_events", "identity")]
    [InlineData(typeof(Customer), "customers", "customers")]
    [InlineData(typeof(CustomerAddress), "customer_addresses", "customers")]
    [InlineData(typeof(LoyaltyTransaction), "loyalty_transactions", "customers")]
    [InlineData(typeof(LoyaltySettings), "loyalty_settings", "customers")]
    public void Entities_ShouldMapToExpectedTableAndSchema(Type entityType, string expectedTable, string expectedSchema)
    {
        var model = CreateModel();
        var entity = model.FindEntityType(entityType);

        Assert.NotNull(entity);
        Assert.Equal(expectedTable, entity.GetTableName());
        Assert.Equal(expectedSchema, entity.GetSchema());
    }

    [Fact]
    public void CompositeKeys_ShouldBeConfiguredCorrectly()
    {
        var model = CreateModel();

        var userRole = model.FindEntityType(typeof(UserRole))!;
        var userRolePk = userRole.FindPrimaryKey();
        Assert.NotNull(userRolePk);
        Assert.Equal(2, userRolePk.Properties.Count);
        Assert.Contains(userRolePk.Properties, p => p.Name == nameof(UserRole.UserId));
        Assert.Contains(userRolePk.Properties, p => p.Name == nameof(UserRole.RoleId));

        var rolePermission = model.FindEntityType(typeof(RolePermission))!;
        var rolePermissionPk = rolePermission.FindPrimaryKey();
        Assert.NotNull(rolePermissionPk);
        Assert.Equal(2, rolePermissionPk.Properties.Count);
        Assert.Contains(rolePermissionPk.Properties, p => p.Name == nameof(RolePermission.RoleId));
        Assert.Contains(rolePermissionPk.Properties, p => p.Name == nameof(RolePermission.PermissionId));
    }

    [Fact]
    public void IdentityUniqueIndexes_ShouldBeConfiguredCorrectly()
    {
        var model = CreateModel();

        var user = model.FindEntityType(typeof(User))!;
        var usernameIndex = user.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(User.Username)));
        Assert.NotNull(usernameIndex);
        Assert.True(usernameIndex.IsUnique);

        var emailIndex = user.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(User.Email)));
        Assert.NotNull(emailIndex);
        Assert.True(emailIndex.IsUnique);

        var role = model.FindEntityType(typeof(Role))!;
        var roleCodeIndex = role.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Role.Code)));
        Assert.NotNull(roleCodeIndex);
        Assert.True(roleCodeIndex.IsUnique);

        var permission = model.FindEntityType(typeof(Permission))!;
        var permCodeIndex = permission.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Permission.Code)));
        Assert.NotNull(permCodeIndex);
        Assert.True(permCodeIndex.IsUnique);

        var refreshToken = model.FindEntityType(typeof(RefreshToken))!;
        var tokenHashIndex = refreshToken.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(RefreshToken.TokenHash)));
        Assert.NotNull(tokenHashIndex);
        Assert.True(tokenHashIndex.IsUnique);
    }

    [Fact]
    public void CustomerUniqueAndIdempotencyIndexes_ShouldBeConfiguredCorrectly()
    {
        var model = CreateModel();

        var customer = model.FindEntityType(typeof(Customer))!;
        var phoneIndex = customer.GetIndexes().FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(Customer.Phone)));
        Assert.NotNull(phoneIndex);
        Assert.True(phoneIndex.IsUnique);

        var loyaltyTx = model.FindEntityType(typeof(LoyaltyTransaction))!;
        var idempotencyIndex = loyaltyTx.GetIndexes()
            .FirstOrDefault(i => i.Properties.Any(p => p.Name == nameof(LoyaltyTransaction.OrderId)) &&
                                 i.Properties.Any(p => p.Name == nameof(LoyaltyTransaction.Type)));

        Assert.NotNull(idempotencyIndex);
        Assert.True(idempotencyIndex.IsUnique);
        Assert.Equal("order_id IS NOT NULL", idempotencyIndex.GetFilter());
    }

    [Fact]
    public void CheckConstraints_ShouldBeConfiguredOnCustomersAndLoyalty()
    {
        var model = CreateModel();

        var customer = model.FindEntityType(typeof(Customer))!;
        var customerConstraints = customer.GetCheckConstraints();
        Assert.Contains(customerConstraints, c => c.Name == "CK_customers_loyalty_points_balance");

        var loyaltyTx = model.FindEntityType(typeof(LoyaltyTransaction))!;
        var txConstraints = loyaltyTx.GetCheckConstraints();
        Assert.Contains(txConstraints, c => c.Name == "CK_loyalty_transactions_type");
        Assert.Contains(txConstraints, c => c.Name == "CK_loyalty_transactions_points");
        Assert.Contains(txConstraints, c => c.Name == "CK_loyalty_transactions_balance_after");

        var loyaltySettings = model.FindEntityType(typeof(LoyaltySettings))!;
        var settingsConstraints = loyaltySettings.GetCheckConstraints();
        Assert.Contains(settingsConstraints, c => c.Name == "CK_loyalty_settings_max_redeemable_percentage");
    }

    [Fact]
    public void ColumnLengthsAndPrecisions_ShouldMatchOpenSpec()
    {
        var model = CreateModel();

        var user = model.FindEntityType(typeof(User))!;
        Assert.Equal(50, user.FindProperty(nameof(User.Username))!.GetMaxLength());
        Assert.Equal(255, user.FindProperty(nameof(User.Email))!.GetMaxLength());
        Assert.Equal(255, user.FindProperty(nameof(User.PasswordHash))!.GetMaxLength());
        Assert.Equal(150, user.FindProperty(nameof(User.FullName))!.GetMaxLength());
        Assert.Equal(30, user.FindProperty(nameof(User.Phone))!.GetMaxLength());

        var customer = model.FindEntityType(typeof(Customer))!;
        Assert.Equal(120, customer.FindProperty(nameof(Customer.FullName))!.GetMaxLength());
        Assert.Equal(30, customer.FindProperty(nameof(Customer.Phone))!.GetMaxLength());

        var loyaltySettings = model.FindEntityType(typeof(LoyaltySettings))!;
        var earnRate = loyaltySettings.FindProperty(nameof(LoyaltySettings.EarnPointsPerCurrencyUnit))!;
        Assert.Equal(8, earnRate.GetPrecision());
        Assert.Equal(4, earnRate.GetScale());

        var redeemRate = loyaltySettings.FindProperty(nameof(LoyaltySettings.RedeemValuePerPoint))!;
        Assert.Equal(8, redeemRate.GetPrecision());
        Assert.Equal(4, redeemRate.GetScale());

        var maxPct = loyaltySettings.FindProperty(nameof(LoyaltySettings.MaxRedeemablePercentage))!;
        Assert.Equal(5, maxPct.GetPrecision());
        Assert.Equal(2, maxPct.GetScale());
    }

    [Fact]
    public void CascadeAndRestrictDeleteBehaviors_ShouldBeConfiguredCorrectly()
    {
        var model = CreateModel();

        var userRole = model.FindEntityType(typeof(UserRole))!;
        var userFk = userRole.GetForeignKeys().First(fk => fk.PrincipalEntityType.ClrType == typeof(User) && fk.Properties.Any(p => p.Name == nameof(UserRole.UserId)));
        Assert.Equal(DeleteBehavior.Cascade, userFk.DeleteBehavior);

        var rolePermission = model.FindEntityType(typeof(RolePermission))!;
        var roleFk = rolePermission.GetForeignKeys().First(fk => fk.PrincipalEntityType.ClrType == typeof(Role));
        Assert.Equal(DeleteBehavior.Cascade, roleFk.DeleteBehavior);

        var customerAddress = model.FindEntityType(typeof(CustomerAddress))!;
        var addressCustomerFk = customerAddress.GetForeignKeys().First(fk => fk.PrincipalEntityType.ClrType == typeof(Customer));
        Assert.Equal(DeleteBehavior.Cascade, addressCustomerFk.DeleteBehavior);

        var loyaltyTx = model.FindEntityType(typeof(LoyaltyTransaction))!;
        var txCustomerFk = loyaltyTx.GetForeignKeys().First(fk => fk.PrincipalEntityType.ClrType == typeof(Customer));
        Assert.Equal(DeleteBehavior.Restrict, txCustomerFk.DeleteBehavior);
    }
}
