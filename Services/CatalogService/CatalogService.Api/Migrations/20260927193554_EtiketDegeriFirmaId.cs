using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class EtiketDegeriFirmaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FirmaId",
                table: "EtiketDegerleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtiketDegerleri_FirmaId",
                table: "EtiketDegerleri",
                column: "FirmaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EtiketDegerleri_FirmaId",
                table: "EtiketDegerleri");

            migrationBuilder.DropColumn(
                name: "FirmaId",
                table: "EtiketDegerleri");
        }
    }
}
