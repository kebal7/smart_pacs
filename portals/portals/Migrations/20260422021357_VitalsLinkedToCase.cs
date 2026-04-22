using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace portals.Migrations
{
    /// <inheritdoc />
    public partial class VitalsLinkedToCase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CaseId",
                table: "Vitals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Vitals_CaseId",
                table: "Vitals",
                column: "CaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Vitals_ClinicalCases_CaseId",
                table: "Vitals",
                column: "CaseId",
                principalTable: "ClinicalCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Vitals_ClinicalCases_CaseId", table: "Vitals");
            migrationBuilder.DropIndex(name: "IX_Vitals_CaseId", table: "Vitals");
            migrationBuilder.DropColumn(name: "CaseId", table: "Vitals");
        }
    }
}
