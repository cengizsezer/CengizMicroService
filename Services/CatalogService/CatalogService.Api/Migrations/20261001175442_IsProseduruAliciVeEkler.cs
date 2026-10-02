using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class IsProseduruAliciVeEkler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MenuYolu",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NasilYapilir",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OnAdimiOlduguIsId",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "Program",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "ProgramDiger",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YasalMukellefiyetKodu",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FirmaIsiAlicilari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaIsiId = table.Column<int>(type: "int", nullable: false),
                    AdSoyad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Eposta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AliciTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaIsiAlicilari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmaIsiAlicilari_FirmaIsleri_FirmaIsiId",
                        column: x => x.FirmaIsiId,
                        principalSchema: "catalog",
                        principalTable: "FirmaIsleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FirmaIsiEkleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaIsiId = table.Column<int>(type: "int", nullable: false),
                    FileId = table.Column<int>(type: "int", nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Boyut = table.Column<long>(type: "bigint", nullable: false),
                    YuklemeZamani = table.Column<DateTime>(type: "datetime2", nullable: false),
                    YukleyenKullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    YukleyenKullaniciAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaIsiEkleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmaIsiEkleri_FirmaIsleri_FirmaIsiId",
                        column: x => x.FirmaIsiId,
                        principalSchema: "catalog",
                        principalTable: "FirmaIsleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsleri_FirmaId_YasalMukellefiyetKodu_Tekrar",
                schema: "catalog",
                table: "FirmaIsleri",
                columns: new[] { "FirmaId", "YasalMukellefiyetKodu", "Tekrar" },
                unique: true,
                filter: "[YasalMukellefiyetKodu] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsleri_OnAdimiOlduguIsId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "OnAdimiOlduguIsId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsiAlicilari_FirmaIsiId",
                schema: "catalog",
                table: "FirmaIsiAlicilari",
                column: "FirmaIsiId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsiEkleri_FirmaIsiId",
                schema: "catalog",
                table: "FirmaIsiEkleri",
                column: "FirmaIsiId");

            migrationBuilder.AddForeignKey(
                name: "FK_FirmaIsleri_FirmaIsleri_OnAdimiOlduguIsId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "OnAdimiOlduguIsId",
                principalSchema: "catalog",
                principalTable: "FirmaIsleri",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirmaIsleri_FirmaIsleri_OnAdimiOlduguIsId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropTable(
                name: "FirmaIsiAlicilari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "FirmaIsiEkleri",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_FirmaIsleri_FirmaId_YasalMukellefiyetKodu_Tekrar",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropIndex(
                name: "IX_FirmaIsleri_OnAdimiOlduguIsId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "MenuYolu",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "NasilYapilir",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "OnAdimiOlduguIsId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "Program",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "ProgramDiger",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "YasalMukellefiyetKodu",
                schema: "catalog",
                table: "FirmaIsleri");
        }
    }
}
