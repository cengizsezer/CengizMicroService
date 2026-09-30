using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AnasayfaFirmaSorumlusu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SorumluKullaniciAdi",
                table: "Firmalar",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SorumluKullaniciId",
                table: "Firmalar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Firmalar_SorumluKullaniciId",
                table: "Firmalar",
                column: "SorumluKullaniciId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Firmalar_SorumluKullaniciId",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "SorumluKullaniciAdi",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "SorumluKullaniciId",
                table: "Firmalar");
        }
    }
}
