using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace portals.Migrations
{
    /// <inheritdoc />
    public partial class CaseItemLinkCaseIdForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseItemLinks_ClinicalCases_ClinicalCaseId",
                table: "CaseItemLinks");

            migrationBuilder.DropIndex(
                name: "IX_CaseItemLinks_ClinicalCaseId",
                table: "CaseItemLinks");

            migrationBuilder.DropColumn(
                name: "ClinicalCaseId",
                table: "CaseItemLinks");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItemLinks_CaseId",
                table: "CaseItemLinks",
                column: "CaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_CaseItemLinks_ClinicalCases_CaseId",
                table: "CaseItemLinks",
                column: "CaseId",
                principalTable: "ClinicalCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseItemLinks_ClinicalCases_CaseId",
                table: "CaseItemLinks");

            migrationBuilder.DropIndex(
                name: "IX_CaseItemLinks_CaseId",
                table: "CaseItemLinks");

            migrationBuilder.AddColumn<int>(
                name: "ClinicalCaseId",
                table: "CaseItemLinks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseItemLinks_ClinicalCaseId",
                table: "CaseItemLinks",
                column: "ClinicalCaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_CaseItemLinks_ClinicalCases_ClinicalCaseId",
                table: "CaseItemLinks",
                column: "ClinicalCaseId",
                principalTable: "ClinicalCases",
                principalColumn: "Id");
        }
    }
}
