using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdfUsageDownloader.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DailyUsage",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EntryTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ElectricityUnits = table.Column<double>(type: "double", nullable: false),
                    ElectricityCost = table.Column<double>(type: "double", nullable: false),
                    ElectricityEstimated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GasUnits = table.Column<double>(type: "double", nullable: false),
                    GasCost = table.Column<double>(type: "double", nullable: false),
                    GasEstimated = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyUsage", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyUsage");
        }
    }
}
