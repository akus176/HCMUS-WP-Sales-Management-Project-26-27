using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class AddOrderInventoryRestorationGuard : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "order_inventory_restorations",
            schema: "inventory",
            columns: table => new
            {
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                restored_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                restored_by = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_inventory_restorations", x => x.order_id);
                table.ForeignKey(
                    name: "FK_order_inventory_restorations_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "sales",
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_order_inventory_restorations_users_restored_by",
                    column: x => x.restored_by,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_order_inventory_restorations_restored_by",
            schema: "inventory",
            table: "order_inventory_restorations",
            column: "restored_by");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_inventory_restorations",
            schema: "inventory");
    }
}
