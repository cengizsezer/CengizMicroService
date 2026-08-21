using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBankaEkstreDuzeltmeleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EkstreOgrenmeKayitlari düşürülüyor: anahtarı ham açıklamanın hash'iydi ve
            // banka her satıra farklı sorgu numarası/tarih/tutar yazdığı için o anahtar
            // asla ikinci kez eşleşmiyordu. Yerine EkstreHesapEslesmeleri (firma bazlı,
            // unvan çekirdeği anahtarlı) + EkstreKimlikKayitlari (global kimlik) geldi.
            // Taşınacak anlamlı veri yok — eski kayıtlar zaten hiç isabet etmiyordu.
            migrationBuilder.DropTable(
                name: "EkstreOgrenmeKayitlari",
                schema: "catalog");

            migrationBuilder.AddColumn<int>(
                name: "AciklamaKolonu",
                schema: "catalog",
                table: "EkstreYuklemeler",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<byte[]>(
                name: "DosyaIcerik",
                schema: "catalog",
                table: "EkstreYuklemeler",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Adaylar",
                schema: "catalog",
                table: "EkstreSatirlari",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnahtarCekirdek",
                schema: "catalog",
                table: "EkstreSatirlari",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AyirtEdiciEk",
                schema: "catalog",
                table: "EkstreSatirlari",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EslesenKarsiSatirId",
                schema: "catalog",
                table: "EkstreSatirlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KaynakSatirNo",
                schema: "catalog",
                table: "EkstreSatirlari",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "SonGuncelleme",
                schema: "catalog",
                table: "EkstreHesapPlani",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IbanKatmaniAktif",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VknKatmaniAktif",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EkstreHesapEslesmeleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnahtarCekirdek = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AyirtEdiciEk = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    AnahtarTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    HesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Yon = table.Column<byte>(type: "tinyint", nullable: false),
                    KullanimSayisi = table.Column<int>(type: "int", nullable: false),
                    SonKullanim = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreHesapEslesmeleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkstreKimlikKayitlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anahtar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AnahtarTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    NormalizeUnvan = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    KullanimSayisi = table.Column<int>(type: "int", nullable: false),
                    SonKullanim = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreKimlikKayitlari", x => x.Id);
                });

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
                name: "IX_EkstreKimlikKayitlari_AnahtarTipi_Anahtar",
                schema: "catalog",
                table: "EkstreKimlikKayitlari",
                columns: new[] { "AnahtarTipi", "Anahtar" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkstreHesapEslesmeleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "EkstreKimlikKayitlari",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "AciklamaKolonu",
                schema: "catalog",
                table: "EkstreYuklemeler");

            migrationBuilder.DropColumn(
                name: "DosyaIcerik",
                schema: "catalog",
                table: "EkstreYuklemeler");

            migrationBuilder.DropColumn(
                name: "Adaylar",
                schema: "catalog",
                table: "EkstreSatirlari");

            migrationBuilder.DropColumn(
                name: "AnahtarCekirdek",
                schema: "catalog",
                table: "EkstreSatirlari");

            migrationBuilder.DropColumn(
                name: "AyirtEdiciEk",
                schema: "catalog",
                table: "EkstreSatirlari");

            migrationBuilder.DropColumn(
                name: "EslesenKarsiSatirId",
                schema: "catalog",
                table: "EkstreSatirlari");

            migrationBuilder.DropColumn(
                name: "KaynakSatirNo",
                schema: "catalog",
                table: "EkstreSatirlari");

            migrationBuilder.DropColumn(
                name: "SonGuncelleme",
                schema: "catalog",
                table: "EkstreHesapPlani");

            migrationBuilder.DropColumn(
                name: "IbanKatmaniAktif",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.DropColumn(
                name: "VknKatmaniAktif",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            migrationBuilder.CreateTable(
                name: "EkstreOgrenmeKayitlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anahtar = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AnahtarTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    KullanimSayisi = table.Column<int>(type: "int", nullable: false),
                    SonKullanim = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Yon = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreOgrenmeKayitlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreOgrenmeKayitlari_TenantNo_AnahtarTipi_Anahtar_Yon",
                schema: "catalog",
                table: "EkstreOgrenmeKayitlari",
                columns: new[] { "TenantNo", "AnahtarTipi", "Anahtar", "Yon" },
                unique: true);
        }
    }
}
