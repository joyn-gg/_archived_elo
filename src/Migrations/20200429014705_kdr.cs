using Microsoft.EntityFrameworkCore.Migrations;

namespace ELO.Migrations
{
    public partial class kdr : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Deaths",
                table: "Players",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Kills",
                table: "Players",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deaths",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Kills",
                table: "Players");
        }
    }
}
