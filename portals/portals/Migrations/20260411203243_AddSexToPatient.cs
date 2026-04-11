using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace portals.Migrations
{
    /// <inheritdoc />
    public partial class AddSexToPatient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "sex",
                table: "patients",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sex",
                table: "patients");
        }
    }
}
