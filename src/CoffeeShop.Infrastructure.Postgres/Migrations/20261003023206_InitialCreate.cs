using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "catalog");

        migrationBuilder.EnsureSchema(
            name: "customers");

        migrationBuilder.EnsureSchema(
            name: "sales");

        migrationBuilder.EnsureSchema(
            name: "inventory");

        migrationBuilder.EnsureSchema(
            name: "identity");

        migrationBuilder.AlterDatabase()
            .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

        migrationBuilder.CreateTable(
            name: "addons",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_addons", x => x.id);
                table.CheckConstraint("CK_addons_price", "price >= 0");
            });

        migrationBuilder.CreateTable(
            name: "categories",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "customers",
            schema: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                loyalty_points_balance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customers", x => x.id);
                table.CheckConstraint("CK_customers_loyalty_points_balance", "loyalty_points_balance >= 0");
            });

        migrationBuilder.CreateTable(
            name: "ingredients",
            schema: "inventory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                base_unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                available_quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false, defaultValue: 0m),
                reorder_threshold = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false, defaultValue: 0m),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ingredients", x => x.id);
                table.CheckConstraint("CK_ingredients_available_quantity", "available_quantity >= 0");
                table.CheckConstraint("CK_ingredients_reorder_threshold", "reorder_threshold >= 0");
            });

        migrationBuilder.CreateTable(
            name: "loyalty_settings",
            schema: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                earn_points_per_currency_unit = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                redeem_value_per_point = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                max_redeemable_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_loyalty_settings", x => x.id);
                table.CheckConstraint("CK_loyalty_settings_max_redeemable_percentage", "max_redeemable_percentage >= 0 AND max_redeemable_percentage <= 100");
            });

        migrationBuilder.CreateTable(
            name: "permissions",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                description = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_permissions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "roles",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                description = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_roles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "suppliers",
            schema: "inventory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_suppliers", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                username = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                deactivated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "menu_items",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                category_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_menu_items", x => x.id);
                table.ForeignKey(
                    name: "FK_menu_items_categories_category_id",
                    column: x => x.category_id,
                    principalSchema: "catalog",
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "customer_addresses",
            schema: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                address_line = table.Column<string>(type: "text", nullable: false),
                is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customer_addresses", x => x.id);
                table.ForeignKey(
                    name: "FK_customer_addresses_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "customers",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "loyalty_transactions",
            schema: "customers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: true),
                type = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                points = table.Column<int>(type: "integer", nullable: false),
                balance_after = table.Column<int>(type: "integer", nullable: false),
                reverses_transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_loyalty_transactions", x => x.id);
                table.CheckConstraint("CK_loyalty_transactions_balance_after", "balance_after >= 0");
                table.CheckConstraint("CK_loyalty_transactions_points", "points <> 0");
                table.CheckConstraint("CK_loyalty_transactions_type", "type IN ('earn', 'redeem', 'reverse_earn', 'reverse_redeem')");
                table.ForeignKey(
                    name: "FK_loyalty_transactions_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "customers",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_loyalty_transactions_loyalty_transactions_reverses_transact~",
                    column: x => x.reverses_transaction_id,
                    principalSchema: "customers",
                    principalTable: "loyalty_transactions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "stock_movements",
            schema: "inventory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<int>(type: "integer", nullable: false),
                quantity_delta = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                reason = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                purchase_receipt_id = table.Column<Guid>(type: "uuid", nullable: true),
                order_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_stock_movements", x => x.id);
                table.CheckConstraint("CK_stock_movements_quantity_delta", "quantity_delta <> 0");
                table.CheckConstraint("CK_stock_movements_type", "type IN (0, 1, 2, 3)");
                table.ForeignKey(
                    name: "FK_stock_movements_ingredients_ingredient_id",
                    column: x => x.ingredient_id,
                    principalSchema: "inventory",
                    principalTable: "ingredients",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "role_permissions",
            schema: "identity",
            columns: table => new
            {
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                permission_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_role_permissions", x => new { x.role_id, x.permission_id });
                table.ForeignKey(
                    name: "FK_role_permissions_permissions_permission_id",
                    column: x => x.permission_id,
                    principalSchema: "identity",
                    principalTable: "permissions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_role_permissions_roles_role_id",
                    column: x => x.role_id,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "purchase_receipts",
            schema: "inventory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                receipt_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                status = table.Column<int>(type: "integer", nullable: false),
                posted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_receipts", x => x.id);
                table.CheckConstraint("CK_purchase_receipts_status", "status IN (0, 1, 2)");
                table.ForeignKey(
                    name: "FK_purchase_receipts_suppliers_supplier_id",
                    column: x => x.supplier_id,
                    principalSchema: "inventory",
                    principalTable: "suppliers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                family_id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_by_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refresh_tokens", x => x.id);
                table.ForeignKey(
                    name: "FK_refresh_tokens_refresh_tokens_replaced_by_id",
                    column: x => x.replaced_by_id,
                    principalSchema: "identity",
                    principalTable: "refresh_tokens",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_refresh_tokens_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "security_audit_events",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: true),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                metadata = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_security_audit_events", x => x.id);
                table.ForeignKey(
                    name: "FK_security_audit_events_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "user_roles",
            schema: "identity",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<Guid>(type: "uuid", nullable: false),
                assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                assigned_by = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role_id });
                table.ForeignKey(
                    name: "FK_user_roles_roles_role_id",
                    column: x => x.role_id,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_roles_users_assigned_by",
                    column: x => x.assigned_by,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_user_roles_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "menu_item_addons",
            schema: "catalog",
            columns: table => new
            {
                menu_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                addon_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_menu_item_addons", x => new { x.menu_item_id, x.addon_id });
                table.ForeignKey(
                    name: "FK_menu_item_addons_addons_addon_id",
                    column: x => x.addon_id,
                    principalSchema: "catalog",
                    principalTable: "addons",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_menu_item_addons_menu_items_menu_item_id",
                    column: x => x.menu_item_id,
                    principalSchema: "catalog",
                    principalTable: "menu_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "size_variants",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                menu_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                size_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_size_variants", x => x.id);
                table.CheckConstraint("CK_size_variants_price", "price >= 0");
                table.ForeignKey(
                    name: "FK_size_variants_menu_items_menu_item_id",
                    column: x => x.menu_item_id,
                    principalSchema: "catalog",
                    principalTable: "menu_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "purchase_receipt_lines",
            schema: "inventory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                unit_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                purchase_receipt_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_receipt_lines", x => x.id);
                table.CheckConstraint("CK_purchase_receipt_lines_quantity", "quantity > 0");
                table.ForeignKey(
                    name: "FK_purchase_receipt_lines_ingredients_ingredient_id",
                    column: x => x.ingredient_id,
                    principalSchema: "inventory",
                    principalTable: "ingredients",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_purchase_receipt_lines_purchase_receipts_purchase_receipt_id",
                    column: x => x.purchase_receipt_id,
                    principalSchema: "inventory",
                    principalTable: "purchase_receipts",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "recipe_ingredients",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                size_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_recipe_ingredients", x => x.id);
                table.CheckConstraint("CK_recipe_ingredients_quantity", "quantity > 0");
                table.ForeignKey(
                    name: "FK_recipe_ingredients_ingredients_ingredient_id",
                    column: x => x.ingredient_id,
                    principalSchema: "inventory",
                    principalTable: "ingredients",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_recipe_ingredients_size_variants_size_variant_id",
                    column: x => x.size_variant_id,
                    principalSchema: "catalog",
                    principalTable: "size_variants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "deliveries",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                recipient_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                contact_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                address_line = table.Column<string>(type: "text", nullable: false),
                customer_address_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValue: "unassigned"),
                assigned_staff_id = table.Column<Guid>(type: "uuid", nullable: true),
                assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                out_for_delivery_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                delivered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                failed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                failure_reason = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deliveries", x => x.id);
                table.CheckConstraint("CK_deliveries_status", "status IN ('unassigned', 'assigned', 'out_for_delivery', 'delivered', 'failed')");
                table.ForeignKey(
                    name: "FK_deliveries_customer_addresses_customer_address_id",
                    column: x => x.customer_address_id,
                    principalSchema: "customers",
                    principalTable: "customer_addresses",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_deliveries_users_assigned_staff_id",
                    column: x => x.assigned_staff_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "discounts",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                value = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "pending"),
                requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                requested_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                approved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                computed_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_discounts", x => x.id);
                table.CheckConstraint("CK_discounts_computed_amount", "computed_amount IS NULL OR computed_amount >= 0");
                table.CheckConstraint("CK_discounts_status", "status IN ('pending', 'approved', 'rejected')");
                table.CheckConstraint("CK_discounts_type", "type IN ('fixed', 'percentage')");
                table.CheckConstraint("CK_discounts_value", "value > 0");
                table.ForeignKey(
                    name: "FK_discounts_users_approved_by",
                    column: x => x.approved_by,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_discounts_users_requested_by",
                    column: x => x.requested_by,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_line_addons",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                addon_id = table.Column<Guid>(type: "uuid", nullable: true),
                addon_name_snapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                addon_price_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_line_addons", x => x.id);
                table.CheckConstraint("CK_order_line_addons_price_snapshot", "addon_price_snapshot >= 0");
                table.ForeignKey(
                    name: "FK_order_line_addons_addons_addon_id",
                    column: x => x.addon_id,
                    principalSchema: "catalog",
                    principalTable: "addons",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_lines",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                menu_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                size_variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                item_name_snapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                size_name_snapshot = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                unit_price_snapshot = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                line_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_lines", x => x.id);
                table.CheckConstraint("CK_order_lines_line_total", "line_total >= 0");
                table.CheckConstraint("CK_order_lines_quantity", "quantity > 0");
                table.CheckConstraint("CK_order_lines_unit_price_snapshot", "unit_price_snapshot >= 0");
                table.ForeignKey(
                    name: "FK_order_lines_menu_items_menu_item_id",
                    column: x => x.menu_item_id,
                    principalSchema: "catalog",
                    principalTable: "menu_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_lines_size_variants_size_variant_id",
                    column: x => x.size_variant_id,
                    principalSchema: "catalog",
                    principalTable: "size_variants",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "order_status_history",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                from_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                to_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_status_history", x => x.id);
                table.CheckConstraint("CK_order_status_history_to_status", "to_status IN ('draft', 'confirmed', 'preparing', 'ready', 'completed', 'cancelled')");
                table.ForeignKey(
                    name: "FK_order_status_history_users_changed_by",
                    column: x => x.changed_by,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "orders",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                order_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValue: "draft"),
                customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                table_id = table.Column<Guid>(type: "uuid", nullable: true),
                subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                discount_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                paid_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                loyalty_points_redeemed = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                loyalty_discount_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, defaultValue: 0m),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                confirmed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                completed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                cancel_reason = table.Column<string>(type: "text", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_orders", x => x.id);
                table.CheckConstraint("CK_orders_channel", "channel IN ('dine_in', 'takeaway', 'delivery')");
                table.CheckConstraint("CK_orders_discount_total", "discount_total >= 0");
                table.CheckConstraint("CK_orders_loyalty_discount_amount", "loyalty_discount_amount >= 0");
                table.CheckConstraint("CK_orders_loyalty_points_redeemed", "loyalty_points_redeemed >= 0");
                table.CheckConstraint("CK_orders_paid_amount", "paid_amount >= 0");
                table.CheckConstraint("CK_orders_status", "status IN ('draft', 'confirmed', 'preparing', 'ready', 'completed', 'cancelled')");
                table.CheckConstraint("CK_orders_total", "total >= 0");
                table.ForeignKey(
                    name: "FK_orders_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "customers",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_orders_users_created_by",
                    column: x => x.created_by,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "payments",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "succeeded"),
                reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                paid_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payments", x => x.id);
                table.CheckConstraint("CK_payments_amount", "amount > 0");
                table.CheckConstraint("CK_payments_method", "method IN ('cash', 'card', 'e_wallet')");
                table.CheckConstraint("CK_payments_status", "status IN ('pending', 'succeeded', 'failed')");
                table.ForeignKey(
                    name: "FK_payments_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "sales",
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_payments_users_recorded_by",
                    column: x => x.recorded_by,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "tables",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                table_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                seats = table.Column<int>(type: "integer", nullable: true),
                is_occupied = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                active_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tables", x => x.id);
                table.ForeignKey(
                    name: "FK_tables_orders_active_order_id",
                    column: x => x.active_order_id,
                    principalSchema: "sales",
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "reservations",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                table_id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                guest_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                contact_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                party_size = table.Column<int>(type: "integer", nullable: false),
                reserved_for = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false, defaultValue: "pending"),
                created_by = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false, defaultValueSql: "now()"),
                cancelled_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reservations", x => x.id);
                table.CheckConstraint("CK_reservations_party_size", "party_size > 0");
                table.CheckConstraint("CK_reservations_status", "status IN ('pending', 'confirmed', 'seated', 'cancelled', 'completed')");
                table.ForeignKey(
                    name: "FK_reservations_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "customers",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_reservations_tables_table_id",
                    column: x => x.table_id,
                    principalSchema: "sales",
                    principalTable: "tables",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_reservations_users_created_by",
                    column: x => x.created_by,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_customer_addresses_customer_id",
            schema: "customers",
            table: "customer_addresses",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "IX_customers_phone",
            schema: "customers",
            table: "customers",
            column: "phone",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_deliveries_assigned_staff_id",
            schema: "sales",
            table: "deliveries",
            column: "assigned_staff_id");

        migrationBuilder.CreateIndex(
            name: "IX_deliveries_customer_address_id",
            schema: "sales",
            table: "deliveries",
            column: "customer_address_id");

        migrationBuilder.CreateIndex(
            name: "IX_deliveries_order_id",
            schema: "sales",
            table: "deliveries",
            column: "order_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_deliveries_status",
            schema: "sales",
            table: "deliveries",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_discounts_approved_by",
            schema: "sales",
            table: "discounts",
            column: "approved_by");

        migrationBuilder.CreateIndex(
            name: "IX_discounts_order_id_status",
            schema: "sales",
            table: "discounts",
            columns: new[] { "order_id", "status" });

        migrationBuilder.CreateIndex(
            name: "IX_discounts_requested_by",
            schema: "sales",
            table: "discounts",
            column: "requested_by");

        migrationBuilder.CreateIndex(
            name: "IX_ingredients_is_active",
            schema: "inventory",
            table: "ingredients",
            column: "is_active");

        migrationBuilder.CreateIndex(
            name: "IX_ingredients_name",
            schema: "inventory",
            table: "ingredients",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_loyalty_transactions_customer_id_created_at",
            schema: "customers",
            table: "loyalty_transactions",
            columns: new[] { "customer_id", "created_at" });

        migrationBuilder.CreateIndex(
            name: "IX_loyalty_transactions_order_id_type",
            schema: "customers",
            table: "loyalty_transactions",
            columns: new[] { "order_id", "type" },
            unique: true,
            filter: "order_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_loyalty_transactions_reverses_transaction_id",
            schema: "customers",
            table: "loyalty_transactions",
            column: "reverses_transaction_id");

        migrationBuilder.CreateIndex(
            name: "IX_menu_item_addons_addon_id",
            schema: "catalog",
            table: "menu_item_addons",
            column: "addon_id");

        migrationBuilder.CreateIndex(
            name: "IX_menu_items_category_id",
            schema: "catalog",
            table: "menu_items",
            column: "category_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_line_addons_addon_id",
            schema: "sales",
            table: "order_line_addons",
            column: "addon_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_line_addons_order_line_id",
            schema: "sales",
            table: "order_line_addons",
            column: "order_line_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_lines_menu_item_id",
            schema: "sales",
            table: "order_lines",
            column: "menu_item_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_lines_order_id",
            schema: "sales",
            table: "order_lines",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_lines_size_variant_id",
            schema: "sales",
            table: "order_lines",
            column: "size_variant_id");

        migrationBuilder.CreateIndex(
            name: "IX_order_status_history_changed_by",
            schema: "sales",
            table: "order_status_history",
            column: "changed_by");

        migrationBuilder.CreateIndex(
            name: "IX_order_status_history_order_id_changed_at",
            schema: "sales",
            table: "order_status_history",
            columns: new[] { "order_id", "changed_at" });

        migrationBuilder.CreateIndex(
            name: "IX_orders_channel_created_at",
            schema: "sales",
            table: "orders",
            columns: new[] { "channel", "created_at" });

        migrationBuilder.CreateIndex(
            name: "IX_orders_created_by",
            schema: "sales",
            table: "orders",
            column: "created_by");

        migrationBuilder.CreateIndex(
            name: "IX_orders_customer_id",
            schema: "sales",
            table: "orders",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "IX_orders_order_number",
            schema: "sales",
            table: "orders",
            column: "order_number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_orders_status",
            schema: "sales",
            table: "orders",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "IX_orders_table_id",
            schema: "sales",
            table: "orders",
            column: "table_id");

        migrationBuilder.CreateIndex(
            name: "IX_payments_order_id",
            schema: "sales",
            table: "payments",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "IX_payments_recorded_by",
            schema: "sales",
            table: "payments",
            column: "recorded_by");

        migrationBuilder.CreateIndex(
            name: "IX_permissions_code",
            schema: "identity",
            table: "permissions",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_purchase_receipt_lines_ingredient_id",
            schema: "inventory",
            table: "purchase_receipt_lines",
            column: "ingredient_id");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_receipt_lines_purchase_receipt_id",
            schema: "inventory",
            table: "purchase_receipt_lines",
            column: "purchase_receipt_id");

        migrationBuilder.CreateIndex(
            name: "IX_purchase_receipts_receipt_number",
            schema: "inventory",
            table: "purchase_receipts",
            column: "receipt_number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_purchase_receipts_supplier_id",
            schema: "inventory",
            table: "purchase_receipts",
            column: "supplier_id");

        migrationBuilder.CreateIndex(
            name: "IX_recipe_ingredients_ingredient_id",
            schema: "catalog",
            table: "recipe_ingredients",
            column: "ingredient_id");

        migrationBuilder.CreateIndex(
            name: "IX_recipe_ingredients_size_variant_id_ingredient_id",
            schema: "catalog",
            table: "recipe_ingredients",
            columns: new[] { "size_variant_id", "ingredient_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_family_id",
            schema: "identity",
            table: "refresh_tokens",
            column: "family_id");

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_replaced_by_id",
            schema: "identity",
            table: "refresh_tokens",
            column: "replaced_by_id");

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_token_hash",
            schema: "identity",
            table: "refresh_tokens",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_user_id",
            schema: "identity",
            table: "refresh_tokens",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "IX_reservations_created_by",
            schema: "sales",
            table: "reservations",
            column: "created_by");

        migrationBuilder.CreateIndex(
            name: "IX_reservations_customer_id",
            schema: "sales",
            table: "reservations",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "IX_reservations_status_reserved_for",
            schema: "sales",
            table: "reservations",
            columns: new[] { "status", "reserved_for" });

        migrationBuilder.CreateIndex(
            name: "IX_reservations_table_id_reserved_for",
            schema: "sales",
            table: "reservations",
            columns: new[] { "table_id", "reserved_for" });

        migrationBuilder.CreateIndex(
            name: "IX_role_permissions_permission_id",
            schema: "identity",
            table: "role_permissions",
            column: "permission_id");

        migrationBuilder.CreateIndex(
            name: "IX_roles_code",
            schema: "identity",
            table: "roles",
            column: "code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_security_audit_events_event_type_occurred_at",
            schema: "identity",
            table: "security_audit_events",
            columns: new[] { "event_type", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "IX_security_audit_events_user_id_occurred_at",
            schema: "identity",
            table: "security_audit_events",
            columns: new[] { "user_id", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "IX_size_variants_menu_item_id_size_name",
            schema: "catalog",
            table: "size_variants",
            columns: new[] { "menu_item_id", "size_name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_stock_movements_ingredient_id_occurred_at",
            schema: "inventory",
            table: "stock_movements",
            columns: new[] { "ingredient_id", "occurred_at" });

        migrationBuilder.CreateIndex(
            name: "IX_stock_movements_order_id",
            schema: "inventory",
            table: "stock_movements",
            column: "order_id",
            filter: "order_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_stock_movements_purchase_receipt_id",
            schema: "inventory",
            table: "stock_movements",
            column: "purchase_receipt_id",
            filter: "purchase_receipt_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_suppliers_name",
            schema: "inventory",
            table: "suppliers",
            column: "name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_tables_active_order_id",
            schema: "sales",
            table: "tables",
            column: "active_order_id",
            unique: true,
            filter: "active_order_id IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_tables_table_number",
            schema: "sales",
            table: "tables",
            column: "table_number",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_roles_assigned_by",
            schema: "identity",
            table: "user_roles",
            column: "assigned_by");

        migrationBuilder.CreateIndex(
            name: "IX_user_roles_role_id",
            schema: "identity",
            table: "user_roles",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "IX_users_email",
            schema: "identity",
            table: "users",
            column: "email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_username",
            schema: "identity",
            table: "users",
            column: "username",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_deliveries_orders_order_id",
            schema: "sales",
            table: "deliveries",
            column: "order_id",
            principalSchema: "sales",
            principalTable: "orders",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_discounts_orders_order_id",
            schema: "sales",
            table: "discounts",
            column: "order_id",
            principalSchema: "sales",
            principalTable: "orders",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_order_line_addons_order_lines_order_line_id",
            schema: "sales",
            table: "order_line_addons",
            column: "order_line_id",
            principalSchema: "sales",
            principalTable: "order_lines",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_order_lines_orders_order_id",
            schema: "sales",
            table: "order_lines",
            column: "order_id",
            principalSchema: "sales",
            principalTable: "orders",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_order_status_history_orders_order_id",
            schema: "sales",
            table: "order_status_history",
            column: "order_id",
            principalSchema: "sales",
            principalTable: "orders",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_orders_tables_table_id",
            schema: "sales",
            table: "orders",
            column: "table_id",
            principalSchema: "sales",
            principalTable: "tables",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_orders_customers_customer_id",
            schema: "sales",
            table: "orders");

        migrationBuilder.DropForeignKey(
            name: "FK_tables_orders_active_order_id",
            schema: "sales",
            table: "tables");

        migrationBuilder.DropTable(
            name: "deliveries",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "discounts",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "loyalty_settings",
            schema: "customers");

        migrationBuilder.DropTable(
            name: "loyalty_transactions",
            schema: "customers");

        migrationBuilder.DropTable(
            name: "menu_item_addons",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "order_line_addons",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "order_status_history",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "payments",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "purchase_receipt_lines",
            schema: "inventory");

        migrationBuilder.DropTable(
            name: "recipe_ingredients",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "refresh_tokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "reservations",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "role_permissions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "security_audit_events",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "stock_movements",
            schema: "inventory");

        migrationBuilder.DropTable(
            name: "user_roles",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "customer_addresses",
            schema: "customers");

        migrationBuilder.DropTable(
            name: "addons",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "order_lines",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "purchase_receipts",
            schema: "inventory");

        migrationBuilder.DropTable(
            name: "permissions",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "ingredients",
            schema: "inventory");

        migrationBuilder.DropTable(
            name: "roles",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "size_variants",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "suppliers",
            schema: "inventory");

        migrationBuilder.DropTable(
            name: "menu_items",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "categories",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "customers",
            schema: "customers");

        migrationBuilder.DropTable(
            name: "orders",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "tables",
            schema: "sales");

        migrationBuilder.DropTable(
            name: "users",
            schema: "identity");
    }
}
