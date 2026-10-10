using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class AddPaymentWorkflowRules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "CK_payments_required_reference",
            schema: "sales",
            table: "payments",
            sql: "method = 'cash' OR (reference IS NOT NULL AND btrim(reference) <> '')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_payments_required_reference",
            schema: "sales",
            table: "payments");
    }
}
