using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class AddLoyaltyWorkflowConstraints : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_loyalty_transactions_reverses_transaction_id",
            schema: "customers",
            table: "loyalty_transactions");

        migrationBuilder.CreateIndex(
            name: "IX_loyalty_transactions_reverses_transaction_id",
            schema: "customers",
            table: "loyalty_transactions",
            column: "reverses_transaction_id",
            unique: true,
            filter: "reverses_transaction_id IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_loyalty_transactions_orders_order_id",
            schema: "customers",
            table: "loyalty_transactions",
            column: "order_id",
            principalSchema: "sales",
            principalTable: "orders",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_loyalty_transactions_orders_order_id",
            schema: "customers",
            table: "loyalty_transactions");

        migrationBuilder.DropIndex(
            name: "IX_loyalty_transactions_reverses_transaction_id",
            schema: "customers",
            table: "loyalty_transactions");

        migrationBuilder.CreateIndex(
            name: "IX_loyalty_transactions_reverses_transaction_id",
            schema: "customers",
            table: "loyalty_transactions",
            column: "reverses_transaction_id");
    }
}
