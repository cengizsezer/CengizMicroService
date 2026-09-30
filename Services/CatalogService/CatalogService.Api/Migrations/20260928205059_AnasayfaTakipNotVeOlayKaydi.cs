using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AnasayfaTakipNotVeOlayKaydi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmaNotlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Metin = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OlusturanKullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OlusturanKullaniciAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OlusturmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaNotlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmaNotlari_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FirmaOlayKayitlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    OlayTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    KullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    KullaniciAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Zaman = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaOlayKayitlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmaOlayKayitlari_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaNotlari_FirmaId_OlusturmaZamani",
                schema: "catalog",
                table: "FirmaNotlari",
                columns: new[] { "FirmaId", "OlusturmaZamani" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaOlayKayitlari_FirmaId_Zaman",
                schema: "catalog",
                table: "FirmaOlayKayitlari",
                columns: new[] { "FirmaId", "Zaman" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmaNotlari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "FirmaOlayKayitlari",
                schema: "catalog");
        }
    }
}
