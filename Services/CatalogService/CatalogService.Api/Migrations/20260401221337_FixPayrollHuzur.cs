using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixPayrollHuzur : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MinimumWageIncomeTaxExemptionMonthly",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumWageStampTaxExemptionMonthly",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RetiredSgkEmployeeRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RetiredUnemploymentEmployeeRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumWageIncomeTaxExemptionMonthly",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "MinimumWageStampTaxExemptionMonthly",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "RetiredSgkEmployeeRate",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "RetiredUnemploymentEmployeeRate",
                schema: "pkf",
                table: "PayrollParameters");
        }
    }
}
