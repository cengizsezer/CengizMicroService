using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollEmployerCostAnd5510Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Incentive05510TreasuryRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgkCeilingMultiplier",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgkEmployerGSSRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgkEmployerKVSKRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgkEmployerMYO05510Rate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SgkEmployerMYORate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnemploymentEmployerRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Incentive05510TreasuryRate",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "SgkCeilingMultiplier",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "SgkEmployerGSSRate",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "SgkEmployerKVSKRate",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "SgkEmployerMYO05510Rate",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "SgkEmployerMYORate",
                schema: "pkf",
                table: "PayrollParameters");

            migrationBuilder.DropColumn(
                name: "UnemploymentEmployerRate",
                schema: "pkf",
                table: "PayrollParameters");
        }
    }
}
