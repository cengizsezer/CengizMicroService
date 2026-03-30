using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    public partial class FixPayrollColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('pkf.PayrollParameters', 'MinimumWageGrossAmount') IS NULL
BEGIN
    ALTER TABLE [pkf].[PayrollParameters]
    ADD [MinimumWageGrossAmount] decimal(18,2) NOT NULL 
    CONSTRAINT [DF_PayrollParameters_MinimumWageGrossAmount] DEFAULT (0.0);
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF COL_LENGTH('pkf.PayrollParameters', 'MinimumWageGrossAmount') IS NOT NULL
BEGIN
    ALTER TABLE [pkf].[PayrollParameters]
    DROP CONSTRAINT [DF_PayrollParameters_MinimumWageGrossAmount];

    ALTER TABLE [pkf].[PayrollParameters]
    DROP COLUMN [MinimumWageGrossAmount];
END
");
        }
    }
}