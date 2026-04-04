using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace portals.Migrations
{
    /// <inheritdoc />
    public partial class AddReportTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "Reports",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalizedBy",
                table: "Reports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GeneratedBy",
                table: "Reports",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsFinalized",
                table: "Reports",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "FinalizedBy",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "GeneratedBy",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "IsFinalized",
                table: "Reports");
        }
    }
}
