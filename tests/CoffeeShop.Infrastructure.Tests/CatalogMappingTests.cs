namespace CoffeeShop.Infrastructure.Tests;

using CoffeeShop.Domain.Catalog;
using CoffeeShop.Domain.Common;
using CoffeeShop.Domain.Inventory;
using CoffeeShop.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

public class CatalogMappingTests
{
    private static CoffeeShopDbContext CreateContext() => new(
        new DbContextOptionsBuilder<CoffeeShopDbContext>()
            .UseNpgsql("Host=localhost;Database=coffeeshop_test")
            .Options);

    private static IEntityType GetEntity<T>()
    {
        using var context = CreateContext();
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(T))!;
    }

    [Theory]
    [InlineData(typeof(Category), "categories", "catalog")]
    [InlineData(typeof(MenuItem), "menu_items", "catalog")]
    [InlineData(typeof(SizeVariant), "size_variants", "catalog")]
    [InlineData(typeof(Addon), "addons", "catalog")]
    [InlineData(typeof(MenuItemAddon), "menu_item_addons", "catalog")]
    [InlineData(typeof(RecipeIngredient), "recipe_ingredients", "catalog")]
    [InlineData(typeof(Ingredient), "ingredients", "inventory")]
    public void Entities_MapToExplicitTablesAndUuidKeys(Type type, string table, string schema)
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;

        Assert.Equal(table, entity.GetTableName());
        Assert.Equal(schema, entity.GetSchema());
        Assert.DoesNotContain(entity.GetProperties(), p => p.IsShadowProperty());
        var key = entity.FindPrimaryKey()!;
        Assert.Equal(type == typeof(MenuItemAddon) ? 2 : 1, key.Properties.Count);
        foreach (var property in key.Properties)
        {
            Assert.False(property.IsNullable);
            Assert.Equal("uuid", property.GetColumnType());
            Assert.Equal(typeof(Guid), property.GetValueConverter()!.ProviderClrType);
            Assert.Equal(type == typeof(MenuItemAddon) ? ValueGenerated.Never : ValueGenerated.OnAdd, property.ValueGenerated);
            Assert.Equal(type == typeof(MenuItemAddon) ? null : "gen_random_uuid()", property.GetDefaultValueSql());
        }

        if (type == typeof(MenuItemAddon))
        {
            Assert.Equal([nameof(MenuItemAddon.MenuItemId), nameof(MenuItemAddon.AddonId)], key.Properties.Select(p => p.Name));
        }
        else
        {
            Assert.Equal("Id", Assert.Single(key.Properties).Name);
        }
    }

    [Theory]
    [InlineData(typeof(Category), "Name", "name", 80, false)]
    [InlineData(typeof(MenuItem), "Name", "name", 120, false)]
    [InlineData(typeof(MenuItem), "Description", "description", null, true)]
    [InlineData(typeof(SizeVariant), "SizeName", "size_name", 40, false)]
    [InlineData(typeof(Addon), "Name", "name", 80, false)]
    [InlineData(typeof(RecipeIngredient), "Unit", "unit", 20, false)]
    [InlineData(typeof(Ingredient), "Name", "name", 100, false)]
    [InlineData(typeof(Ingredient), "BaseUnit", "base_unit", 20, false)]
    public void TextColumns_HaveExplicitNamesLengthsAndNullability(Type type, string name, string column, int? length, bool nullable)
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
        var property = entity.FindProperty(name)!;

        Assert.Equal(column, property.GetColumnName());
        Assert.Equal(length, property.GetMaxLength());
        Assert.Equal(nullable, property.IsNullable);
        Assert.Equal(length.HasValue ? $"character varying({length})" : "text", property.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(SizeVariant), "Price", "price", 12, 2)]
    [InlineData(typeof(Addon), "Price", "price", 12, 2)]
    [InlineData(typeof(RecipeIngredient), "Quantity", "quantity", 12, 3)]
    [InlineData(typeof(Ingredient), "AvailableQuantity", "available_quantity", 14, 3)]
    [InlineData(typeof(Ingredient), "ReorderThreshold", "reorder_threshold", 14, 3)]
    public void DecimalColumns_MatchDesignPrecision(Type type, string name, string column, int precision, int scale)
    {
        using var context = CreateContext();
        var property = context.GetService<IDesignTimeModel>().Model.FindEntityType(type)!.FindProperty(name)!;

        Assert.Equal(column, property.GetColumnName());
        Assert.Equal(precision, property.GetPrecision());
        Assert.Equal(scale, property.GetScale());
        Assert.False(property.IsNullable);
        Assert.Equal($"numeric({precision},{scale})", property.GetColumnType());
        if (name == "Price")
        {
            var converter = property.GetValueConverter()!;
            Assert.Equal(12.34m, converter.ConvertToProvider(new Money(12.34m)));
            Assert.Equal(new Money(12.34m), converter.ConvertFromProvider(12.34m));
            Assert.Equal(Money.Zero, converter.ConvertFromProvider(0m));
            Assert.Equal(new Money(Money.MaximumAmount), converter.ConvertFromProvider(Money.MaximumAmount));
        }
    }

    [Fact]
    public void Indexes_EnforceVariantAndRecipeUniquenessAndSupportForeignKeys()
    {
        Assert.Contains(GetEntity<SizeVariant>().GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(["MenuItemId", "SizeName"]));
        Assert.Contains(GetEntity<RecipeIngredient>().GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(["SizeVariantId", "IngredientId"]));
        Assert.Contains(GetEntity<Ingredient>().GetIndexes(), i => i.IsUnique &&
            i.Properties.Select(p => p.Name).SequenceEqual(["Name"]));
        Assert.Contains(GetEntity<MenuItem>().GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(["CategoryId"]));
        Assert.Contains(GetEntity<MenuItemAddon>().GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(["AddonId"]));
        Assert.Contains(GetEntity<RecipeIngredient>().GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(["IngredientId"]));
        Assert.Contains(GetEntity<Ingredient>().GetIndexes(), i => i.Properties.Select(p => p.Name).SequenceEqual(["IsActive"]));
    }

    [Theory]
    [InlineData(typeof(MenuItem), typeof(Category), "CategoryId", "category_id", DeleteBehavior.Restrict)]
    [InlineData(typeof(SizeVariant), typeof(MenuItem), "MenuItemId", "menu_item_id", DeleteBehavior.Restrict)]
    [InlineData(typeof(MenuItemAddon), typeof(MenuItem), "MenuItemId", "menu_item_id", DeleteBehavior.Cascade)]
    [InlineData(typeof(MenuItemAddon), typeof(Addon), "AddonId", "addon_id", DeleteBehavior.Cascade)]
    [InlineData(typeof(RecipeIngredient), typeof(SizeVariant), "SizeVariantId", "size_variant_id", DeleteBehavior.Restrict)]
    [InlineData(typeof(RecipeIngredient), typeof(Ingredient), "IngredientId", "ingredient_id", DeleteBehavior.Restrict)]
    public void Relationships_AreRequiredWithExplicitDeleteBehavior(Type dependent, Type principal, string name, string column, DeleteBehavior behavior)
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(dependent)!;
        var fk = Assert.Single(entity.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == principal);

        Assert.True(fk.IsRequired);
        Assert.False(fk.IsUnique);
        Assert.Equal(behavior, fk.DeleteBehavior);
        var property = Assert.Single(fk.Properties);
        Assert.Equal(name, property.Name);
        Assert.Equal(column, property.GetColumnName());
        Assert.Equal("uuid", property.GetColumnType());
        var id = EntityId.New();
        Assert.Equal(id.Value, property.GetValueConverter()!.ConvertToProvider(id));
        Assert.Equal(id, property.GetValueConverter()!.ConvertFromProvider(id.Value));
    }

    [Theory]
    [InlineData(typeof(SizeVariant), "CK_size_variants_price", "price >= 0")]
    [InlineData(typeof(Addon), "CK_addons_price", "price >= 0")]
    [InlineData(typeof(RecipeIngredient), "CK_recipe_ingredients_quantity", "quantity > 0")]
    [InlineData(typeof(Ingredient), "CK_ingredients_available_quantity", "available_quantity >= 0")]
    [InlineData(typeof(Ingredient), "CK_ingredients_reorder_threshold", "reorder_threshold >= 0")]
    public void CheckConstraints_ProtectNumericRanges(Type type, string name, string sql)
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
        Assert.Equal(sql, Assert.Single(entity.GetCheckConstraints(), c => c.Name == name).Sql);
    }

    [Theory]
    [InlineData(typeof(Category))]
    [InlineData(typeof(MenuItem))]
    [InlineData(typeof(SizeVariant))]
    [InlineData(typeof(Addon))]
    [InlineData(typeof(Ingredient))]
    public void ActivationAndTimestampDefaults_AreExplicit(Type type)
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(type)!;
        var active = entity.FindProperty("IsActive")!;
        Assert.Equal("is_active", active.GetColumnName());
        Assert.Equal(true, active.GetDefaultValue());
        Assert.False(active.IsNullable);

        if (type == typeof(Category) || type == typeof(MenuItem) || type == typeof(Ingredient))
        {
            foreach (var name in new[] { "CreatedAt", "UpdatedAt" })
            {
                var timestamp = entity.FindProperty(name);
                Assert.NotNull(timestamp);
                Assert.False(timestamp.IsNullable);
                Assert.Equal("timestamptz", timestamp.GetColumnType());
                Assert.Equal("now()", timestamp.GetDefaultValueSql());
                Assert.Equal(typeof(DateTimeOffset), timestamp.GetValueConverter()!.ProviderClrType);
            }
        }

        if (type == typeof(Category))
        {
            Assert.Equal(0, entity.FindProperty("DisplayOrder")!.GetDefaultValue());
        }
        if (type == typeof(Ingredient))
        {
            Assert.Equal(0m, entity.FindProperty("AvailableQuantity")!.GetDefaultValue());
            Assert.Equal(0m, entity.FindProperty("ReorderThreshold")!.GetDefaultValue());
        }
    }

    [Fact]
    public void PostgreSqlDdl_ContainsCatalogConstraintsAndCrossSchemaRecipeForeignKey()
    {
        using var context = CreateContext();
        var sql = context.Database.GenerateCreateScript();

        foreach (var table in new[] { "categories", "menu_items", "size_variants", "addons", "menu_item_addons", "recipe_ingredients" })
        {
            Assert.Contains($"CREATE TABLE catalog.{table}", sql);
        }
        Assert.Contains("CREATE TABLE inventory.ingredients", sql);
        Assert.Contains("id uuid NOT NULL DEFAULT (gen_random_uuid())", sql);
        Assert.Contains("price numeric(12,2) NOT NULL", sql);
        Assert.Contains("quantity numeric(12,3) NOT NULL", sql);
        Assert.Contains("CHECK (quantity > 0)", sql);
        Assert.Contains("PRIMARY KEY (menu_item_id, addon_id)", sql);
        Assert.Contains("FOREIGN KEY (ingredient_id) REFERENCES inventory.ingredients (id) ON DELETE RESTRICT", sql);
        Assert.Contains("ON catalog.size_variants (menu_item_id, size_name)", sql);
        Assert.Contains("ON catalog.recipe_ingredients (size_variant_id, ingredient_id)", sql);
    }

    [Fact]
    public void Deactivation_TracksOnlyStateAndTimestampChangesWithoutDeletingRelationships()
    {
        using var context = CreateContext();
        var created = new UtcTimestamp(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero));
        var changed = new UtcTimestamp(created.Value.AddHours(1));
        var category = new Category(EntityId.New(), "Coffee", created, created);
        var item = new MenuItem(EntityId.New(), category, "Latte", created, created);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m));
        var addon = new Addon(EntityId.New(), "Extra shot", new Money(10000m));
        var compatibility = new MenuItemAddon(item.Id, addon.Id);
        context.AttachRange(category, item, variant, addon, compatibility);

        category.Deactivate(changed);
        item.Deactivate(changed);
        context.ChangeTracker.DetectChanges();

        foreach (var entry in context.ChangeTracker.Entries().Where(entry => entry.Entity is Category or MenuItem))
        {
            Assert.Equal(EntityState.Modified, entry.State);
            Assert.Equal(["IsActive", "UpdatedAt"], entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name));
        }

        Assert.Equal(EntityState.Unchanged, context.Entry(variant).State);
        Assert.Equal(EntityState.Unchanged, context.Entry(addon).State);
        Assert.Equal(EntityState.Unchanged, context.Entry(compatibility).State);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), entry => entry.State == EntityState.Deleted);
        Assert.Equal(category.Id, item.CategoryId);
        Assert.Equal(item.Id, variant.MenuItemId);
        Assert.Equal(item.Id, compatibility.MenuItemId);
    }

    [Fact]
    public void VariantUpdates_TrackOnlyPriceNameAndAvailabilityAndPreserveRecipe()
    {
        using var context = CreateContext();
        var variant = new SizeVariant(EntityId.New(), EntityId.New(), "Large", new Money(45000m));
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var ingredient = new Ingredient(EntityId.New(), "Coffee", "g", now, now);
        var recipe = new RecipeIngredient(EntityId.New(), variant, ingredient, 18m, "g");
        context.AttachRange(variant, recipe);

        variant.Update("Extra large", new Money(50000m));
        variant.Deactivate();
        context.ChangeTracker.DetectChanges();

        var entry = context.Entry(variant);
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.Equal(["IsActive", "Price", "SizeName"], entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name));
        Assert.Equal(new Money(45000m), entry.Property(v => v.Price).OriginalValue);
        Assert.Equal(new Money(50000m), entry.Property(v => v.Price).CurrentValue);
        Assert.False(entry.Property(v => v.IsActive).CurrentValue);
        Assert.Equal(EntityState.Unchanged, context.Entry(recipe).State);
        Assert.Equal(variant.Id, recipe.SizeVariantId);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), tracked => tracked.State == EntityState.Deleted);
    }

    [Fact]
    public void AddonUpdates_TrackOnlyNamePriceAndAvailabilityAndPreserveCompatibility()
    {
        using var context = CreateContext();
        var addon = new Addon(EntityId.New(), "Extra shot", new Money(10000m));
        var compatibility = new MenuItemAddon(EntityId.New(), addon.Id);
        context.AttachRange(addon, compatibility);

        addon.Update("Double shot", new Money(20000m));
        addon.Deactivate();
        context.ChangeTracker.DetectChanges();

        var entry = context.Entry(addon);
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.Equal(["IsActive", "Name", "Price"], entry.Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name));
        Assert.Equal(new Money(10000m), entry.Property(a => a.Price).OriginalValue);
        Assert.Equal(new Money(20000m), entry.Property(a => a.Price).CurrentValue);
        Assert.Equal(EntityState.Unchanged, context.Entry(compatibility).State);
        Assert.Equal(addon.Id, compatibility.AddonId);
        Assert.DoesNotContain(context.ChangeTracker.Entries(), tracked => tracked.State == EntityState.Deleted);
    }

    [Fact]
    public void CompleteCatalogGraph_CanBeTrackedWithoutShadowRelationships()
    {
        using var context = CreateContext();
        var now = new UtcTimestamp(DateTimeOffset.UtcNow);
        var category = new Category(EntityId.New(), "Coffee", now, now);
        var item = new MenuItem(EntityId.New(), category, "Latte", now, now);
        var variant = new SizeVariant(EntityId.New(), item.Id, "Large", new Money(45000m), isActive: false);
        var addon = new Addon(EntityId.New(), "Extra shot", new Money(10000m));
        var ingredient = new Ingredient(EntityId.New(), "Coffee beans", "g", now, now);
        var recipe = new RecipeIngredient(EntityId.New(), variant, ingredient, 18.5m, "g");
        var compatibility = new MenuItemAddon(item.Id, addon.Id);

        context.AddRange(category, item, variant, addon, ingredient, recipe, compatibility);

        Assert.Equal(7, context.ChangeTracker.Entries().Count());
        Assert.All(context.ChangeTracker.Entries(), entry => Assert.Equal(EntityState.Added, entry.State));
        Assert.False(variant.IsActive);
        Assert.False(context.Entry(variant).Property(v => v.IsActive).CurrentValue);
        Assert.Contains("catalog.recipe_ingredients", context.RecipeIngredients
            .Where(r => r.SizeVariantId == variant.Id).ToQueryString());
        Assert.Contains("catalog.size_variants", context.SizeVariants
            .Where(v => v.Price == new Money(45000m)).ToQueryString());
    }
}
