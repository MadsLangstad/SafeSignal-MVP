using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeSignal.Cloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllClearWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "first_clearance_at",
                table: "alert_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "first_clearance_user_id",
                table: "alert_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fully_cleared_at",
                table: "alert_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "second_clearance_at",
                table: "alert_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "second_clearance_user_id",
                table: "alert_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alert_clearances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClearanceStep = table.Column<int>(type: "integer", nullable: false),
                    ClearedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "jsonb", nullable: true),
                    DeviceInfo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_clearances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_clearances_alert_history_AlertId",
                        column: x => x.AlertId,
                        principalTable: "alert_history",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_clearances_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_clearances_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_history_first_clearance_user_id_first_clearance_at",
                table: "alert_history",
                columns: new[] { "first_clearance_user_id", "first_clearance_at" },
                filter: "first_clearance_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_alert_history_OrganizationId_Status",
                table: "alert_history",
                columns: new[] { "OrganizationId", "Status" },
                filter: "\"Status\" = 'PendingClearance'");

            migrationBuilder.CreateIndex(
                name: "IX_alert_history_second_clearance_user_id",
                table: "alert_history",
                column: "second_clearance_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_alert_clearances_AlertId",
                table: "alert_clearances",
                column: "AlertId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_clearances_AlertId_ClearanceStep",
                table: "alert_clearances",
                columns: new[] { "AlertId", "ClearanceStep" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_clearances_OrganizationId_ClearedAt",
                table: "alert_clearances",
                columns: new[] { "OrganizationId", "ClearedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_clearances_UserId",
                table: "alert_clearances",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_alert_history_users_first_clearance_user_id",
                table: "alert_history",
                column: "first_clearance_user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_alert_history_users_second_clearance_user_id",
                table: "alert_history",
                column: "second_clearance_user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alert_history_users_first_clearance_user_id",
                table: "alert_history");

            migrationBuilder.DropForeignKey(
                name: "FK_alert_history_users_second_clearance_user_id",
                table: "alert_history");

            migrationBuilder.DropTable(
                name: "alert_clearances");

            migrationBuilder.DropIndex(
                name: "IX_alert_history_first_clearance_user_id_first_clearance_at",
                table: "alert_history");

            migrationBuilder.DropIndex(
                name: "IX_alert_history_OrganizationId_Status",
                table: "alert_history");

            migrationBuilder.DropIndex(
                name: "IX_alert_history_second_clearance_user_id",
                table: "alert_history");

            migrationBuilder.DropColumn(
                name: "first_clearance_at",
                table: "alert_history");

            migrationBuilder.DropColumn(
                name: "first_clearance_user_id",
                table: "alert_history");

            migrationBuilder.DropColumn(
                name: "fully_cleared_at",
                table: "alert_history");

            migrationBuilder.DropColumn(
                name: "second_clearance_at",
                table: "alert_history");

            migrationBuilder.DropColumn(
                name: "second_clearance_user_id",
                table: "alert_history");
        }
    }
}
