using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeShop.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class AddMobileDeviceRegistrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mobile_device_registrations",
            schema: "identity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                last_seen_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mobile_device_registrations", x => x.id);
                table.ForeignKey(
                    name: "FK_mobile_device_registrations_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_mobile_device_registrations_token",
            schema: "identity",
            table: "mobile_device_registrations",
            column: "token",
            unique: true,
            filter: "revoked_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_mobile_device_registrations_user_id",
            schema: "identity",
            table: "mobile_device_registrations",
            column: "user_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "mobile_device_registrations",
            schema: "identity");
    }
}
