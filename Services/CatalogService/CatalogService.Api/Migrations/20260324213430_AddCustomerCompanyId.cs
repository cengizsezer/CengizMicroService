using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerCompanyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerCompanies_TenantNo_CompanyName",
                table: "CustomerCompanies");

            migrationBuilder.AddColumn<int>(
                name: "CustomerCompanyId",
                schema: "catalog",
                table: "Declarations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCompanies_TenantNo_CompanyName_TaxNumber",
                table: "CustomerCompanies",
                columns: new[] { "TenantNo", "CompanyName", "TaxNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomerCompanies_TenantNo_CompanyName_TaxNumber",
                table: "CustomerCompanies");

            migrationBuilder.DropColumn(
                name: "CustomerCompanyId",
                schema: "catalog",
                table: "Declarations");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCompanies_TenantNo_CompanyName",
                table: "CustomerCompanies",
                columns: new[] { "TenantNo", "CompanyName" });
        }
    }
}
