using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AjanIsleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AjanIsleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AjanId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsTipi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Yuk = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    IlerlemeYuzde = table.Column<int>(type: "int", nullable: false),
                    IlerlemeMesaji = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ToplamAdim = table.Column<int>(type: "int", nullable: false),
                    TamamlananAdim = table.Column<int>(type: "int", nullable: false),
                    OlusturanKullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OlusturmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GonderimZamani = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BaslamaZamani = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BitisZamani = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SonIlerlemeZamani = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HataMesaji = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SonucOzeti = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HataEkraniDosyaId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FirmaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AjanIsleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AjanIsleri_AjanId_Durum",
                schema: "catalog",
                table: "AjanIsleri",
                columns: new[] { "AjanId", "Durum" });

            migrationBuilder.CreateIndex(
                name: "IX_AjanIsleri_FirmaId_OlusturmaZamani",
                schema: "catalog",
                table: "AjanIsleri",
                columns: new[] { "FirmaId", "OlusturmaZamani" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AjanIsleri",
                schema: "catalog");
        }
    }
}
