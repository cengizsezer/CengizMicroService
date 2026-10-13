using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddParolalar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParolaErisimKayitlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParolaId = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    FirmaAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Alan = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Islem = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    KullaniciId = table.Column<int>(type: "int", nullable: true),
                    Kullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tarih = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParolaErisimKayitlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parolalar",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    FirmaId = table.Column<int>(type: "int", nullable: true),
                    FirmaAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Entegrator = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PortalAdresi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    KullaniciAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    KullaniciKoduEk = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Sube = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SifreSifreli = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParolaSifreli = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SistemSifresiSifreli = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsyeriSifresiSifreli = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Not = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TekillikAnahtari = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncelleyenKullaniciId = table.Column<int>(type: "int", nullable: true),
                    GuncelleyenKullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parolalar", x => x.Id);
                    table.CheckConstraint("CK_Parolalar_Tur", "[Tur] BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "FK_Parolalar_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParolaErisimKayitlari_ParolaId_Tarih",
                schema: "catalog",
                table: "ParolaErisimKayitlari",
                columns: new[] { "ParolaId", "Tarih" });

            migrationBuilder.CreateIndex(
                name: "IX_ParolaErisimKayitlari_Tarih",
                schema: "catalog",
                table: "ParolaErisimKayitlari",
                column: "Tarih");

            migrationBuilder.CreateIndex(
                name: "IX_Parolalar_FirmaId",
                schema: "catalog",
                table: "Parolalar",
                column: "FirmaId");

            migrationBuilder.CreateIndex(
                name: "IX_Parolalar_TekillikAnahtari",
                schema: "catalog",
                table: "Parolalar",
                column: "TekillikAnahtari",
                unique: true,
                filter: "[TekillikAnahtari] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Parolalar_Tur_FirmaId",
                schema: "catalog",
                table: "Parolalar",
                columns: new[] { "Tur", "FirmaId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParolaErisimKayitlari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Parolalar",
                schema: "catalog");
        }
    }
}
