using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ELO.Migrations
{
    public partial class DeletedPremiumuser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletedPremiumUsers",
                columns: table => new
                {
                    UserId = table.Column<decimal>(nullable: false),
                    EntitledRoleId = table.Column<decimal>(nullable: false),
                    EntitledRegistrationCount = table.Column<int>(nullable: false),
                    LastSuccessfulKnownPayment = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedPremiumUsers", x => x.UserId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeletedPremiumUsers");
        }
    }
}
