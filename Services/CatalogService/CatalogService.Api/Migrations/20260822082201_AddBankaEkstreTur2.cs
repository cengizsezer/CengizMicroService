using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBankaEkstreTur2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdayKumesiOzeti",
                schema: "catalog",
                table: "EkstreSatirlari",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BelirsizlikAnahtari",
                schema: "catalog",
                table: "EkstreSatirlari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdayKumesiOzeti",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HesapSahibiTakmaAdlari",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EkstreVergiKodlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VergiKodu = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AnahtarKelime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreVergiKodlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreVergiKodlari_Sira",
                schema: "catalog",
                table: "EkstreVergiKodlari",
                column: "Sira");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkstreVergiKodlari",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "AdayKumesiOzeti",
                schema: "catalog",
                table: "EkstreSatirlari");

            migrationBuilder.DropColumn(
                name: "BelirsizlikAnahtari",
                schema: "catalog",
                table: "EkstreSatirlari");

            migrationBuilder.DropColumn(
                name: "AdayKumesiOzeti",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri");

            migrationBuilder.DropColumn(
                name: "HesapSahibiTakmaAdlari",
                schema: "catalog",
                table: "EkstreBankaHesaplari");
        }
    }
}
