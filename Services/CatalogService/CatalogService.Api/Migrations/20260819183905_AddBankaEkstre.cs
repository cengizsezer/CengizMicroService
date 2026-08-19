using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBankaEkstre : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EkstreAciklamaSablonlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParserTipi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IslemTipiDeseni = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EslesmeTuru = table.Column<byte>(type: "tinyint", nullable: false),
                    Sablon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BankalarArasi = table.Column<bool>(type: "bit", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreAciklamaSablonlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkstreBankaHesaplari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankaAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HesapTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    OrkaHesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ParserTipi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreBankaHesaplari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkstreHesapPlani",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NormalizeAd = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AnaGrup = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BaslangicHarfi = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreHesapPlani", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkstreOgrenmeKayitlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Anahtar = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
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
                    table.PrimaryKey("PK_EkstreOgrenmeKayitlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkstreSabitKurallar",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParserTipi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IslemTipiDeseni = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EslesmeTuru = table.Column<byte>(type: "tinyint", nullable: false),
                    Yon = table.Column<byte>(type: "tinyint", nullable: true),
                    HesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Guven = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreSabitKurallar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkstreUnvanDesenleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParserTipi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Desen = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    GrupNo = table.Column<int>(type: "int", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreUnvanDesenleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkstreYuklemeler",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BankaHesabiId = table.Column<int>(type: "int", nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    YuklemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DonemBaslangic = table.Column<DateTime>(type: "date", nullable: true),
                    DonemBitis = table.Column<DateTime>(type: "date", nullable: true),
                    SatirSayisi = table.Column<int>(type: "int", nullable: false),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    Uyarilar = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreYuklemeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkstreYuklemeler_EkstreBankaHesaplari_BankaHesabiId",
                        column: x => x.BankaHesabiId,
                        principalSchema: "catalog",
                        principalTable: "EkstreBankaHesaplari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EkstreSatirlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EkstreYuklemeId = table.Column<int>(type: "int", nullable: false),
                    SiraNo = table.Column<int>(type: "int", nullable: false),
                    Tarih = table.Column<DateTime>(type: "date", nullable: false),
                    Yon = table.Column<byte>(type: "tinyint", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IslemTipi = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    HamAciklama = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KarsiIban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    KarsiVkn = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    Kanal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UretilenAciklama = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CikarilanUnvan = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OnerilenHesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OnerilenHesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuvenSkoru = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    KaynakKatman = table.Column<byte>(type: "tinyint", nullable: false),
                    IkinciAdayKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IkinciAdayAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IkinciAdaySkoru = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    OnaylananHesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    OnaylananHesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OnayTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OnaylayanKullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkstreSatirlari_EkstreYuklemeler_EkstreYuklemeId",
                        column: x => x.EkstreYuklemeId,
                        principalSchema: "catalog",
                        principalTable: "EkstreYuklemeler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreAciklamaSablonlari_ParserTipi_Sira",
                schema: "catalog",
                table: "EkstreAciklamaSablonlari",
                columns: new[] { "ParserTipi", "Sira" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreBankaHesaplari_TenantNo_BankaAdi",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                columns: new[] { "TenantNo", "BankaAdi" });

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
                name: "IX_EkstreOgrenmeKayitlari_TenantNo_AnahtarTipi_Anahtar_Yon",
                schema: "catalog",
                table: "EkstreOgrenmeKayitlari",
                columns: new[] { "TenantNo", "AnahtarTipi", "Anahtar", "Yon" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkstreSabitKurallar_ParserTipi_Sira",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                columns: new[] { "ParserTipi", "Sira" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreSatirlari_EkstreYuklemeId_Durum",
                schema: "catalog",
                table: "EkstreSatirlari",
                columns: new[] { "EkstreYuklemeId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreUnvanDesenleri_ParserTipi_Sira",
                schema: "catalog",
                table: "EkstreUnvanDesenleri",
                columns: new[] { "ParserTipi", "Sira" });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreYuklemeler_BankaHesabiId",
                schema: "catalog",
                table: "EkstreYuklemeler",
                column: "BankaHesabiId");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreYuklemeler_TenantNo_BankaHesabiId",
                schema: "catalog",
                table: "EkstreYuklemeler",
                columns: new[] { "TenantNo", "BankaHesabiId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkstreAciklamaSablonlari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "EkstreHesapPlani",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "EkstreOgrenmeKayitlari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "EkstreSabitKurallar",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "EkstreSatirlari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "EkstreUnvanDesenleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "EkstreYuklemeler",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "EkstreBankaHesaplari",
                schema: "catalog");
        }
    }
}
