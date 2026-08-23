using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class BankaOtomasyonFirmaKapsami : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EkstreYuklemeler_TenantNo_BankaHesabiId",
                schema: "catalog",
                table: "EkstreYuklemeler");

            migrationBuilder.DropIndex(
                name: "IX_EkstreKisiYonlendirmeleri_TenantNo_IsimCekirdegi_Yon",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri");

            migrationBuilder.DropIndex(
                name: "IX_EkstreHesapPlani_TenantNo_AnaGrup_BaslangicHarfi",
                schema: "catalog",
                table: "EkstreHesapPlani");

            migrationBuilder.DropIndex(
                name: "IX_EkstreHesapPlani_TenantNo_Kod",
                schema: "catalog",
                table: "EkstreHesapPlani");

            migrationBuilder.DropIndex(
                name: "IX_EkstreHesapEslesmeleri_Cekirdek",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri");

            migrationBuilder.DropIndex(
                name: "IX_EkstreHesapEslesmeleri_CekirdekEk",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri");

            migrationBuilder.DropIndex(
                name: "IX_EkstreBankaHesaplari_TenantNo_BankaAdi",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.DropIndex(
                name: "IX_EkstreBankaHesaplari_TenantNo_OrkaHesapKodu",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.AddColumn<int>(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreYuklemeler",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreHesapPlani",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Eski satırların kapsamı: modül bir dönem veriyi TENANT altına yazdı ve
            // tenant ile firma arasında güvenilir bir eşleme yok (token "500 / PKF Istanbul
            // SMMM" derken kayıtlar aslında PKF Aday'a aitti). Otomatik taşıma hatayı
            // görünmez yapardı, o yüzden HİÇBİR SATIR BİR FİRMAYA ATANMIYOR.
            //
            // Ama hepsine düz 0 yazmak da olmaz: eski şemanın (TenantNo, Kod) tekilliği
            // (0, Kod)'a çöker ve iki tenant'ta aynı kod varsa aşağıdaki unique index'ler
            // migration'ı düşürür. Bu yüzden her tenant, tablolar arasında TUTARLI ve
            // NEGATİF bir sahte kapsam alıyor: gerçek Firma.Id'ler pozitif olduğu için
            // bu satırlar hiçbir firmanın ekranında görünmez, tekillik ise korunur.
            //
            // Bu satırlar Tanımlar > Veri temizliği > "sahipsiz kayıtlar" ile silinir
            // (bkz. KARARLAR §71). FirmaId <= 0 olan her şey sahipsizdir.
            const string sahteKapsam =
                "UPDATE {0} SET FirmaId = -(ABS(CHECKSUM(TenantNo)) % 1000000 + 1) WHERE TenantNo IS NOT NULL AND TenantNo <> ''";

            foreach (var tablo in new[]
                     {
                         "catalog.EkstreBankaHesaplari",
                         "catalog.EkstreYuklemeler",
                         "catalog.EkstreHesapPlani",
                         "catalog.EkstreHesapEslesmeleri",
                         "catalog.EkstreKisiYonlendirmeleri"
                     })
            {
                migrationBuilder.Sql(string.Format(sahteKapsam, tablo));
            }

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreYuklemeler");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreHesapPlani");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreYuklemeler_FirmaId_BankaHesabiId",
                schema: "catalog",
                table: "EkstreYuklemeler",
                columns: new[] { "FirmaId", "BankaHesabiId" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreKisiYonlendirmeleri_FirmaId_IsimCekirdegi_Yon",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri",
                columns: new[] { "FirmaId", "IsimCekirdegi", "Yon" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkstreHesapPlani_FirmaId_AnaGrup_BaslangicHarfi",
                schema: "catalog",
                table: "EkstreHesapPlani",
                columns: new[] { "FirmaId", "AnaGrup", "BaslangicHarfi" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreHesapPlani_FirmaId_Kod",
                schema: "catalog",
                table: "EkstreHesapPlani",
                columns: new[] { "FirmaId", "Kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkstreHesapEslesmeleri_Cekirdek",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri",
                columns: new[] { "FirmaId", "AnahtarTipi", "AnahtarCekirdek", "Yon" },
                unique: true,
                filter: "[AyirtEdiciEk] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreHesapEslesmeleri_CekirdekEk",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri",
                columns: new[] { "FirmaId", "AnahtarTipi", "AnahtarCekirdek", "AyirtEdiciEk", "Yon" },
                unique: true,
                filter: "[AyirtEdiciEk] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreBankaHesaplari_FirmaId_BankaAdi",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                columns: new[] { "FirmaId", "BankaAdi" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreBankaHesaplari_FirmaId_OrkaHesapKodu",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                columns: new[] { "FirmaId", "OrkaHesapKodu" },
                unique: true);
        }

        /// <summary>
        /// Geri alma şemayı geri getirir ama VERİYİ GERİ GETİRMEZ: TenantNo boş string
        /// olarak yazılır, dolayısıyla eski (TenantNo, Kod) tekilliği tek satırdan fazla
        /// kayıt varsa sağlanamaz. Bu migration ileri yönde kullanılmak üzere yazıldı.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EkstreYuklemeler_FirmaId_BankaHesabiId",
                schema: "catalog",
                table: "EkstreYuklemeler");

            migrationBuilder.DropIndex(
                name: "IX_EkstreKisiYonlendirmeleri_FirmaId_IsimCekirdegi_Yon",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri");

            migrationBuilder.DropIndex(
                name: "IX_EkstreHesapPlani_FirmaId_AnaGrup_BaslangicHarfi",
                schema: "catalog",
                table: "EkstreHesapPlani");

            migrationBuilder.DropIndex(
                name: "IX_EkstreHesapPlani_FirmaId_Kod",
                schema: "catalog",
                table: "EkstreHesapPlani");

            migrationBuilder.DropIndex(
                name: "IX_EkstreHesapEslesmeleri_Cekirdek",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri");

            migrationBuilder.DropIndex(
                name: "IX_EkstreHesapEslesmeleri_CekirdekEk",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri");

            migrationBuilder.DropIndex(
                name: "IX_EkstreBankaHesaplari_FirmaId_BankaAdi",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.DropIndex(
                name: "IX_EkstreBankaHesaplari_FirmaId_OrkaHesapKodu",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreYuklemeler");

            migrationBuilder.DropColumn(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri");

            migrationBuilder.DropColumn(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreHesapPlani");

            migrationBuilder.DropColumn(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri");

            migrationBuilder.DropColumn(
                name: "FirmaId",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreYuklemeler",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreHesapPlani",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreYuklemeler_TenantNo_BankaHesabiId",
                schema: "catalog",
                table: "EkstreYuklemeler",
                columns: new[] { "TenantNo", "BankaHesabiId" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreKisiYonlendirmeleri_TenantNo_IsimCekirdegi_Yon",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri",
                columns: new[] { "TenantNo", "IsimCekirdegi", "Yon" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkstreHesapPlani_TenantNo_AnaGrup_BaslangicHarfi",
                schema: "catalog",
                table: "EkstreHesapPlani",
                columns: new[] { "TenantNo", "AnaGrup", "BaslangicHarfi" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreHesapPlani_TenantNo_Kod",
                schema: "catalog",
                table: "EkstreHesapPlani",
                columns: new[] { "TenantNo", "Kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkstreHesapEslesmeleri_Cekirdek",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri",
                columns: new[] { "TenantNo", "AnahtarTipi", "AnahtarCekirdek", "Yon" },
                unique: true,
                filter: "[AyirtEdiciEk] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreHesapEslesmeleri_CekirdekEk",
                schema: "catalog",
                table: "EkstreHesapEslesmeleri",
                columns: new[] { "TenantNo", "AnahtarTipi", "AnahtarCekirdek", "AyirtEdiciEk", "Yon" },
                unique: true,
                filter: "[AyirtEdiciEk] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreBankaHesaplari_TenantNo_BankaAdi",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                columns: new[] { "TenantNo", "BankaAdi" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreBankaHesaplari_TenantNo_OrkaHesapKodu",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                columns: new[] { "TenantNo", "OrkaHesapKodu" },
                unique: true);
        }
    }
}
