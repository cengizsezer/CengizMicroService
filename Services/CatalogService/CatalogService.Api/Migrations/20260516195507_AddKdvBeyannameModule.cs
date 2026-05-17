using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddKdvBeyannameModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TelefonAlanKodu",
                table: "Firmalar",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VergiDairesiKodu",
                table: "Firmalar",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YetkiliAdi",
                table: "Firmalar",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YetkiliSoyadi",
                table: "Firmalar",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "GelenFaturalar",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    FaturaNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GondericiVkn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GondericiUnvan = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    FaturaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParaBirimi = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MatrahTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToplamTutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    KdvOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SonGuncelleme = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Kaynak = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GelenFaturalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GelenFaturalar_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KdvBeyannameMizan",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Donem = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    HesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BorcToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AlacakToplam = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BorcKalan = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AlacakKalan = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KdvBeyannameMizan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KdvBeyannameMizan_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KdvBeyannameTaramalar",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    BaslangicAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BitisAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HataMesaji = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BulunanFaturaSayisi = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KdvBeyannameTaramalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KdvBeyannameTaramalar_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KdvBeyannameYevmiye",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Donem = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    HesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Borc = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Alacak = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FisNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Tarih = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FaturaNo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    BelgeTipi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KdvBeyannameYevmiye", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KdvBeyannameYevmiye_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GelenFaturalar_FirmaId_FaturaNo_GondericiVkn",
                table: "GelenFaturalar",
                columns: new[] { "FirmaId", "FaturaNo", "GondericiVkn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GelenFaturalar_FirmaId_FaturaTarihi",
                table: "GelenFaturalar",
                columns: new[] { "FirmaId", "FaturaTarihi" });

            migrationBuilder.CreateIndex(
                name: "IX_KdvBeyannameMizan_FirmaId_Donem_HesapKodu",
                table: "KdvBeyannameMizan",
                columns: new[] { "FirmaId", "Donem", "HesapKodu" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KdvBeyannameTaramalar_Durum",
                table: "KdvBeyannameTaramalar",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_KdvBeyannameTaramalar_FirmaId_BaslangicAt",
                table: "KdvBeyannameTaramalar",
                columns: new[] { "FirmaId", "BaslangicAt" });

            migrationBuilder.CreateIndex(
                name: "IX_KdvBeyannameYevmiye_FirmaId_Donem",
                table: "KdvBeyannameYevmiye",
                columns: new[] { "FirmaId", "Donem" });

            migrationBuilder.CreateIndex(
                name: "IX_KdvBeyannameYevmiye_FirmaId_Donem_FaturaNo",
                table: "KdvBeyannameYevmiye",
                columns: new[] { "FirmaId", "Donem", "FaturaNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "GelenFaturalar");

            migrationBuilder.DropTable(
                name: "KdvBeyannameMizan");

            migrationBuilder.DropTable(
                name: "KdvBeyannameTaramalar");

            migrationBuilder.DropTable(
                name: "KdvBeyannameYevmiye");

            migrationBuilder.DropColumn(
                name: "TelefonAlanKodu",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "VergiDairesiKodu",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "YetkiliAdi",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "YetkiliSoyadi",
                table: "Firmalar");
        }
    }
}
