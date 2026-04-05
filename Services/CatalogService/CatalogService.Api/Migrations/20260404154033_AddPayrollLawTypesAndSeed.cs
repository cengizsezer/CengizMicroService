using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollLawTypesAndSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "PayrollLawTypes",
                newName: "PayrollLawTypes",
                newSchema: "pkf");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "pkf",
                table: "PayrollLawTypes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "pkf",
                table: "PayrollLawTypes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLawTypes_Year_Code",
                schema: "pkf",
                table: "PayrollLawTypes",
                columns: new[] { "Year", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollLawTypes_Year_Code",
                schema: "pkf",
                table: "PayrollLawTypes");

            migrationBuilder.RenameTable(
                name: "PayrollLawTypes",
                schema: "pkf",
                newName: "PayrollLawTypes");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "PayrollLawTypes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                table: "PayrollLawTypes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }
    }
}
