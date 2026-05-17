using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovos.InvoiceWorker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyFirmaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FirmaId",
                table: "SovosCompanies",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SovosCompanies_FirmaId",
                table: "SovosCompanies",
                column: "FirmaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SovosCompanies_FirmaId",
                table: "SovosCompanies");

            migrationBuilder.DropColumn(
                name: "FirmaId",
                table: "SovosCompanies");
        }
    }
}
