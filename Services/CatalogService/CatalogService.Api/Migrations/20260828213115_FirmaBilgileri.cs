using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class FirmaBilgileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmaBelgeleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    FileId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    YukleyenKullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaBelgeleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmaImzaYetkilileri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tckn = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    Gorev = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TemsilSekli = table.Column<byte>(type: "tinyint", nullable: false),
                    YetkiBaslangic = table.Column<DateTime>(type: "datetime2", nullable: true),
                    YetkiBitis = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Not = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaImzaYetkilileri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmaOrtaklari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TcknVkn = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    PayTutari = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayOrani = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Not = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaOrtaklari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirmaSicilBilgileri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    MersisNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    KurulusTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Adres = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    NaceKodu = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Sermaye = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SermayeParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaSicilBilgileri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaBelgeleri_FirmaId_Tur",
                schema: "catalog",
                table: "FirmaBelgeleri",
                columns: new[] { "FirmaId", "Tur" });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaImzaYetkilileri_FirmaId",
                schema: "catalog",
                table: "FirmaImzaYetkilileri",
                column: "FirmaId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaOrtaklari_FirmaId",
                schema: "catalog",
                table: "FirmaOrtaklari",
                column: "FirmaId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaSicilBilgileri_FirmaId",
                schema: "catalog",
                table: "FirmaSicilBilgileri",
                column: "FirmaId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmaBelgeleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "FirmaImzaYetkilileri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "FirmaOrtaklari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "FirmaSicilBilgileri",
                schema: "catalog");
        }
    }
}
