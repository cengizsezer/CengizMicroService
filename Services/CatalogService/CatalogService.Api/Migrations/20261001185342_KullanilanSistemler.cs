using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class KullanilanSistemler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "FirmaTipi",
                table: "Firmalar",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "MuhtasarDonemi",
                table: "Firmalar",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "SgkTesvikKademesi",
                table: "Firmalar",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "SistemNotu",
                table: "Firmalar",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sistemler",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OlusturanKullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sistemler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmaSistemleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    SistemId = table.Column<int>(type: "int", nullable: false),
                    FirmaKodu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaSistemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmaSistemleri_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FirmaSistemleri_Sistemler_SistemId",
                        column: x => x.SistemId,
                        principalSchema: "catalog",
                        principalTable: "Sistemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaSistemleri_FirmaId_SistemId",
                schema: "catalog",
                table: "FirmaSistemleri",
                columns: new[] { "FirmaId", "SistemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirmaSistemleri_SistemId",
                schema: "catalog",
                table: "FirmaSistemleri",
                column: "SistemId");

            migrationBuilder.CreateIndex(
                name: "IX_Sistemler_Tur_Ad",
                schema: "catalog",
                table: "Sistemler",
                columns: new[] { "Tur", "Ad" },
                unique: true);

            // Başlangıç kayıtları (SistemSeed.Kayitlar ile aynı). Migration'da da ekleniyor:
            // FirmaIsiSistemId migration'ı Prompt 8'in program değerlerini bu kayıtlara bağlıyor.
            // Tur: 1 Muhasebe, 2 EFatura, 5 Beyanname.
            migrationBuilder.Sql(@"
INSERT INTO [catalog].[Sistemler] ([Ad], [Tur], [Aktif], [OlusturmaZamani], [OlusturanKullaniciId])
SELECT v.Ad, v.Tur, 1, SYSUTCDATETIME(), N'seed'
FROM (VALUES (N'Luca', 1), (N'ORKA', 1), (N'Logo', 1), (N'Mikro', 1),
             (N'DijitalPlanet', 2), (N'TURMOB', 2), (N'e-Beyanname GİB', 5)) AS v(Ad, Tur)
WHERE NOT EXISTS (SELECT 1 FROM [catalog].[Sistemler] s WHERE s.[Tur] = v.Tur AND s.[Ad] = v.Ad);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmaSistemleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Sistemler",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "FirmaTipi",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "MuhtasarDonemi",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "SgkTesvikKademesi",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "SistemNotu",
                table: "Firmalar");
        }
    }
}
