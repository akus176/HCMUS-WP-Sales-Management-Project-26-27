using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class AddDiscountApprovalRules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "reason",
            schema: "sales",
            table: "discounts",
            type: "text",
            nullable: false,
            defaultValue: "Legacy discount request (reason not recorded)");

        migrationBuilder.Sql("ALTER TABLE sales.discounts ALTER COLUMN reason DROP DEFAULT");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "rejected_at",
            schema: "sales",
            table: "discounts",
            type: "timestamptz",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "rejected_by",
            schema: "sales",
            table: "discounts",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_discounts_rejected_by",
            schema: "sales",
            table: "discounts",
            column: "rejected_by");

        // Old scaffold rows may lack decision metadata. Preserve history without inventing
        // actors or times; enforce this constraint on every new or updated row.
        migrationBuilder.Sql("ALTER TABLE sales.discounts ADD CONSTRAINT \"CK_discounts_decision\" CHECK ((status = 'approved' AND approved_by IS NOT NULL AND approved_at IS NOT NULL AND approved_at >= requested_at AND computed_amount IS NOT NULL AND computed_amount > 0 AND rejected_by IS NULL AND rejected_at IS NULL) OR (status = 'pending' AND approved_by IS NULL AND approved_at IS NULL AND computed_amount IS NULL AND rejected_by IS NULL AND rejected_at IS NULL) OR (status = 'rejected' AND approved_by IS NULL AND approved_at IS NULL AND computed_amount IS NULL AND rejected_by IS NOT NULL AND rejected_at IS NOT NULL AND rejected_at >= requested_at)) NOT VALID");

        migrationBuilder.AddCheckConstraint(
            name: "CK_discounts_percentage",
            schema: "sales",
            table: "discounts",
            sql: "type <> 'percentage' OR value <= 100");

        migrationBuilder.AddCheckConstraint(
            name: "CK_discounts_reason",
            schema: "sales",
            table: "discounts",
            sql: "length(btrim(reason)) > 0");

        migrationBuilder.AddForeignKey(
            name: "FK_discounts_users_rejected_by",
            schema: "sales",
            table: "discounts",
            column: "rejected_by",
            principalSchema: "identity",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_discounts_users_rejected_by",
            schema: "sales",
            table: "discounts");

        migrationBuilder.DropIndex(
            name: "IX_discounts_rejected_by",
            schema: "sales",
            table: "discounts");

        migrationBuilder.DropCheckConstraint(
            name: "CK_discounts_decision",
            schema: "sales",
            table: "discounts");

        migrationBuilder.DropCheckConstraint(
            name: "CK_discounts_percentage",
            schema: "sales",
            table: "discounts");

        migrationBuilder.DropCheckConstraint(
            name: "CK_discounts_reason",
            schema: "sales",
            table: "discounts");

        migrationBuilder.DropColumn(
            name: "reason",
            schema: "sales",
            table: "discounts");

        migrationBuilder.DropColumn(
            name: "rejected_at",
            schema: "sales",
            table: "discounts");

        migrationBuilder.DropColumn(
            name: "rejected_by",
            schema: "sales",
            table: "discounts");
    }
}
