using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovos.InvoiceWorker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduleHour",
                table: "SovosCompanies",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScheduleMode",
                table: "SovosCompanies",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduleHour",
                table: "SovosCompanies");

            migrationBuilder.DropColumn(
                name: "ScheduleMode",
                table: "SovosCompanies");
        }
    }
}
