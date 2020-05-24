using Microsoft.EntityFrameworkCore.Migrations;

namespace ELO.Migrations
{
    public partial class DevWhitelist : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WhitelistedDevelopers",
                columns: table => new
                {
                    UserId = table.Column<decimal>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhitelistedDevelopers", x => x.UserId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhitelistedDevelopers");
        }
    }
}
