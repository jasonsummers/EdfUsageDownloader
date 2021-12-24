using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EdfUsageDownloader.Migrations
{
    public partial class Rename_Date_Column_of_DailyUsageRecord_to_ReadDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Date",
                table: "DailyUsage");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadDate",
                table: "DailyUsage",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadDate",
                table: "DailyUsage");

            migrationBuilder.AddColumn<DateOnly>(
                name: "Date",
                table: "DailyUsage",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));
        }
    }
}
