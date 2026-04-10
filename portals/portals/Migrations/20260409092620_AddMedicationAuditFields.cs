using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace portals.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicationAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DiscontinuedDate",
                table: "Medications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscontinuedReason",
                table: "Medications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PrescribedDate",
                table: "Medications",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscontinuedDate",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "DiscontinuedReason",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "PrescribedDate",
                table: "Medications");
        }
    }
}
