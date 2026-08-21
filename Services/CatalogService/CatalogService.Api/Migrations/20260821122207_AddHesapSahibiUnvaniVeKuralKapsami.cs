using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <summary>
    /// İki düzeltme:
    /// <list type="number">
    /// <item>
    /// Banka hesabına <c>HesapSahibiUnvani</c> — firmanın kendi unvanı. Banka açıklamalarında
    /// hesap sahibinin kendi adı da geçiyor ve unvan çıkarıcı onu karşı taraf sanıyordu.
    /// </item>
    /// <item>
    /// Sabit kurala kapsam alanları. Kural artık ham açıklamada da aranabiliyor (personel
    /// avansı), yalnız ana grubu verip alt hesabı onaya bırakabiliyor ve unvan çıkarmayı
    /// kapatabiliyor.
    /// </item>
    /// </list>
    /// </summary>
    public partial class AddHesapSahibiUnvaniVeKuralKapsami : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EkstreSabitKurallar_ParserTipi_Sira",
                schema: "catalog",
                table: "EkstreSabitKurallar");

            migrationBuilder.AddColumn<bool>(
                name: "AltHesapGerekli",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "Kapsam",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<bool>(
                name: "UnvanCikarilsin",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HesapSahibiUnvani",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkstreSabitKurallar_ParserTipi_Kapsam_Sira",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                columns: new[] { "ParserTipi", "Kapsam", "Sira" });

            // Mevcut kurallar (banka masrafı, HGS) işlem tipine bakar ve unvan çıkarmayı
            // kapatmaz. Sütun varsayılanları CLR varsayılanı olduğu için (Kapsam=0 hiçbir
            // kapsamla eşleşmez, UnvanCikarilsin=false unvan çıkarmayı kapatırdı) eski
            // satırlar burada doğru değerlere çekilir.
            migrationBuilder.Sql(
                "UPDATE [catalog].[EkstreSabitKurallar] SET [Kapsam] = 1, [UnvanCikarilsin] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EkstreSabitKurallar_ParserTipi_Kapsam_Sira",
                schema: "catalog",
                table: "EkstreSabitKurallar");

            migrationBuilder.DropColumn(
                name: "AltHesapGerekli",
                schema: "catalog",
                table: "EkstreSabitKurallar");

            migrationBuilder.DropColumn(
                name: "Kapsam",
                schema: "catalog",
                table: "EkstreSabitKurallar");

            migrationBuilder.DropColumn(
                name: "UnvanCikarilsin",
                schema: "catalog",
                table: "EkstreSabitKurallar");

            migrationBuilder.DropColumn(
                name: "HesapSahibiUnvani",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreSabitKurallar_ParserTipi_Sira",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                columns: new[] { "ParserTipi", "Sira" });
        }
    }
}
