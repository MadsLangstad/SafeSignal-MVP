using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeSignal.Cloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingIdToAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BuildingId",
                table: "alert_history",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_alert_history_BuildingId",
                table: "alert_history",
                column: "BuildingId");

            migrationBuilder.AddForeignKey(
                name: "FK_alert_history_buildings_BuildingId",
                table: "alert_history",
                column: "BuildingId",
                principalTable: "buildings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alert_history_buildings_BuildingId",
                table: "alert_history");

            migrationBuilder.DropIndex(
                name: "IX_alert_history_BuildingId",
                table: "alert_history");

            migrationBuilder.DropColumn(
                name: "BuildingId",
                table: "alert_history");
        }
    }
}
