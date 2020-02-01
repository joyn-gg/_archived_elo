using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ELO.Migrations
{
    public partial class PremiumBuffer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BufferedPremiumCount",
                table: "Competitions",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PremiumBuffer",
                table: "Competitions",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BufferedPremiumCount",
                table: "Competitions");

            migrationBuilder.DropColumn(
                name: "PremiumBuffer",
                table: "Competitions");
        }
    }
}
