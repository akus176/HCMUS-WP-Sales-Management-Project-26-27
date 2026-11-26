using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AllowTableDeletionWithOrderHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_tables_table_id",
                schema: "sales",
                table: "orders");

            migrationBuilder.AddForeignKey(
                name: "FK_orders_tables_table_id",
                schema: "sales",
                table: "orders",
                column: "table_id",
                principalSchema: "sales",
                principalTable: "tables",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_orders_tables_table_id",
                schema: "sales",
                table: "orders");

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
    }
}
