using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmaPortfoyu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AylikMizanlar",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    Ay = table.Column<int>(type: "int", nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    YuklemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    YukleyenKullaniciId = table.Column<int>(type: "int", nullable: true),
                    YukleyenKullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BorcBakiyeToplami = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AlacakBakiyeToplami = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DengeFarki = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Dengeli = table.Column<bool>(type: "bit", nullable: false),
                    KapanisSonrasiMi = table.Column<bool>(type: "bit", nullable: false),
                    GiderYontemi = table.Column<byte>(type: "tinyint", nullable: false),
                    SatirSayisi = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AylikMizanlar", x => x.Id);
                    table.CheckConstraint("CK_AylikMizanlar_Ay", "[Ay] BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_AylikMizanlar_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AylikMizanSatirlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AylikMizanId = table.Column<int>(type: "int", nullable: false),
                    HesapKodu = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    BorcBakiye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AlacakBakiye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Bakiye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AylikMizanSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AylikMizanSatirlari_AylikMizanlar_AylikMizanId",
                        column: x => x.AylikMizanId,
                        principalSchema: "catalog",
                        principalTable: "AylikMizanlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AylikMizanlar_FirmaId_Yil_Ay",
                schema: "catalog",
                table: "AylikMizanlar",
                columns: new[] { "FirmaId", "Yil", "Ay" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AylikMizanlar_Yil_Ay",
                schema: "catalog",
                table: "AylikMizanlar",
                columns: new[] { "Yil", "Ay" });

            migrationBuilder.CreateIndex(
                name: "IX_AylikMizanSatirlari_AylikMizanId_HesapKodu",
                schema: "catalog",
                table: "AylikMizanSatirlari",
                columns: new[] { "AylikMizanId", "HesapKodu" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AylikMizanSatirlari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "AylikMizanlar",
                schema: "catalog");
        }
    }
}
