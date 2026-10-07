using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class FilterActiveReservations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_reservations_table_id_reserved_for",
            schema: "sales",
            table: "reservations");

        migrationBuilder.CreateIndex(
            name: "IX_reservations_table_id_reserved_for",
            schema: "sales",
            table: "reservations",
            columns: new[] { "table_id", "reserved_for" },
            unique: true,
            filter: "status NOT IN ('cancelled', 'completed')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_reservations_table_id_reserved_for",
            schema: "sales",
            table: "reservations");

        migrationBuilder.CreateIndex(
            name: "IX_reservations_table_id_reserved_for",
            schema: "sales",
            table: "reservations",
            columns: new[] { "table_id", "reserved_for" });
    }
}
