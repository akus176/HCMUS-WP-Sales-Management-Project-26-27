using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class RepairDeliveryFulfillmentSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "to_status",
            schema: "sales",
            table: "delivery_status_history",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(15)",
            oldMaxLength: 15);

        migrationBuilder.AlterColumn<string>(
            name: "from_status",
            schema: "sales",
            table: "delivery_status_history",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(15)",
            oldMaxLength: 15);

        migrationBuilder.AlterColumn<string>(
            name: "status",
            schema: "sales",
            table: "deliveries",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "unassigned",
            oldClrType: typeof(string),
            oldType: "character varying(15)",
            oldMaxLength: 15,
            oldDefaultValue: "unassigned");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "to_status",
            schema: "sales",
            table: "delivery_status_history",
            type: "character varying(15)",
            maxLength: 15,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(16)",
            oldMaxLength: 16);

        migrationBuilder.AlterColumn<string>(
            name: "from_status",
            schema: "sales",
            table: "delivery_status_history",
            type: "character varying(15)",
            maxLength: 15,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(16)",
            oldMaxLength: 16);

        migrationBuilder.AlterColumn<string>(
            name: "status",
            schema: "sales",
            table: "deliveries",
            type: "character varying(15)",
            maxLength: 15,
            nullable: false,
            defaultValue: "unassigned",
            oldClrType: typeof(string),
            oldType: "character varying(16)",
            oldMaxLength: 16,
            oldDefaultValue: "unassigned");
    }
}
