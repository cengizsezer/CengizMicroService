using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class FirmaIsiIlkDonem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IlkDonem",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "varchar(7)",
                unicode: false,
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IlkDonem",
                schema: "catalog",
                table: "FirmaIsleri");
        }
    }
}
