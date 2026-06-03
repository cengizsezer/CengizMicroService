using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBankaModulu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hesaplar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Tip = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Siklik = table.Column<int>(type: "int", nullable: false),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hesaplar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hesaplar_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IslemKayitlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HesapId = table.Column<int>(type: "int", nullable: false),
                    Tarih = table.Column<DateTime>(type: "date", nullable: false),
                    IslendiMi = table.Column<bool>(type: "bit", nullable: false),
                    IsleyenKullanici = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IslemZamani = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IslemKayitlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IslemKayitlari_Hesaplar_HesapId",
                        column: x => x.HesapId,
                        principalTable: "Hesaplar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hesaplar_AktifMi",
                table: "Hesaplar",
                column: "AktifMi");

            migrationBuilder.CreateIndex(
                name: "IX_Hesaplar_FirmaId",
                table: "Hesaplar",
                column: "FirmaId");

            migrationBuilder.CreateIndex(
                name: "IX_IslemKayitlari_HesapId_Tarih",
                table: "IslemKayitlari",
                columns: new[] { "HesapId", "Tarih" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IslemKayitlari");

            migrationBuilder.DropTable(
                name: "Hesaplar");
        }
    }
}
