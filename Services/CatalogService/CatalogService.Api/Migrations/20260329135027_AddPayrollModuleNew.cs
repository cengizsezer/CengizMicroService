using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollModuleNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumWageIncomeTaxExemptionMonthly",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.RenameColumn(
                name: "MinimumWageStampTaxExemptionMonthly",
                schema: "pkf",
                table: "PayrollParameters",
                newName: "MinimumWageGrossAmount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MinimumWageGrossAmount",
                schema: "pkf",
                table: "PayrollParameters",
                newName: "MinimumWageStampTaxExemptionMonthly");

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumWageIncomeTaxExemptionMonthly",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
