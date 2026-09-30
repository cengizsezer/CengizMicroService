using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class FirmaIsleriVeYapilacaklar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmaIsleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Tekrar = table.Column<byte>(type: "tinyint", nullable: false),
                    GunKurali = table.Column<byte>(type: "tinyint", nullable: false),
                    AyinGunu = table.Column<int>(type: "int", nullable: true),
                    TekSeferTarih = table.Column<DateTime>(type: "date", nullable: true),
                    SorumluKullaniciId = table.Column<int>(type: "int", nullable: true),
                    SorumluKullaniciAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanKullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    OlusturmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaIsleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmaIsleri_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IsTamamlamalari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KaynakTip = table.Column<byte>(type: "tinyint", nullable: false),
                    KaynakId = table.Column<int>(type: "int", nullable: false),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    DonemAnahtari = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TamamlanmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    KullaniciAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsTamamlamalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsTamamlamalari_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VergiTakvimi",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MukellefiyetKodu = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tekrar = table.Column<byte>(type: "tinyint", nullable: false),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    DonemNo = table.Column<int>(type: "int", nullable: false),
                    DonemBas = table.Column<DateTime>(type: "date", nullable: false),
                    DonemBit = table.Column<DateTime>(type: "date", nullable: false),
                    SonGun = table.Column<DateTime>(type: "date", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VergiTakvimi", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsleri_FirmaId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "FirmaId");

            migrationBuilder.CreateIndex(
                name: "IX_IsTamamlamalari_FirmaId",
                schema: "catalog",
                table: "IsTamamlamalari",
                column: "FirmaId");

            migrationBuilder.CreateIndex(
                name: "IX_IsTamamlamalari_KaynakTip_KaynakId_FirmaId_DonemAnahtari",
                schema: "catalog",
                table: "IsTamamlamalari",
                columns: new[] { "KaynakTip", "KaynakId", "FirmaId", "DonemAnahtari" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VergiTakvimi_MukellefiyetKodu_Tekrar_Yil_DonemNo",
                schema: "catalog",
                table: "VergiTakvimi",
                columns: new[] { "MukellefiyetKodu", "Tekrar", "Yil", "DonemNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VergiTakvimi_SonGun",
                schema: "catalog",
                table: "VergiTakvimi",
                column: "SonGun");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmaIsleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "IsTamamlamalari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "VergiTakvimi",
                schema: "catalog");
        }
    }
}
