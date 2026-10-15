using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

[DbContext(typeof(CoffeeShopDbContext))]
[Migration("20261010060000_AddDeliveryStatusHistory")]
public partial class AddDeliveryStatusHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "delivery_status_history", schema: "sales", columns: table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false),
            delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
            from_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
            to_status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
            actor_id = table.Column<Guid>(type: "uuid", nullable: false),
            changed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_delivery_status_history", x => x.id);
            table.CheckConstraint("CK_delivery_status_history_status", "from_status IN ('unassigned', 'assigned', 'out_for_delivery', 'delivered', 'failed') AND to_status IN ('unassigned', 'assigned', 'out_for_delivery', 'delivered', 'failed')");
            table.ForeignKey("FK_delivery_status_history_deliveries_delivery_id", x => x.delivery_id, principalSchema: "sales", principalTable: "deliveries", principalColumn: "id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_delivery_status_history_users_actor_id", x => x.actor_id, principalSchema: "identity", principalTable: "users", principalColumn: "id", onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex(name: "IX_delivery_status_history_delivery_id_changed_at", schema: "sales", table: "delivery_status_history", columns: new[] { "delivery_id", "changed_at" });
        migrationBuilder.CreateIndex(name: "IX_delivery_status_history_actor_id", schema: "sales", table: "delivery_status_history", column: "actor_id");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "delivery_status_history", schema: "sales");
}
