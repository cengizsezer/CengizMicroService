using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBankaHesabiIceAktarim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HesapAdi: banka hesabının ORKA'daki adı. Toplu içe aktarım dosyasında zorunlu
            // kolon; elle açılmış eski kayıtlarda karşılığı olmadığı için nullable.
            // Benzersiz index: içe aktarımın upsert anahtarı (firma + ORKA kodu). Tekillik
            // zaten servis katmanında kontrol ediliyordu, index yarış durumuna karşı.
            migrationBuilder.AddColumn<string>(
                name: "HesapAdi",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkstreBankaHesaplari_TenantNo_OrkaHesapKodu",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                columns: new[] { "TenantNo", "OrkaHesapKodu" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EkstreBankaHesaplari_TenantNo_OrkaHesapKodu",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "HesapAdi",
                schema: "catalog",
                table: "EkstreBankaHesaplari");
        }
    }
}
