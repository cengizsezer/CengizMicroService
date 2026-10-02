using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class IsTarifleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IsTarifiId",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IsTarifleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SistemId = table.Column<int>(type: "int", nullable: true),
                    MenuYolu = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NasilYapilir = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    OlusturmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsTarifleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsTarifleri_Sistemler_SistemId",
                        column: x => x.SistemId,
                        principalSchema: "catalog",
                        principalTable: "Sistemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IsTarifiEkleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsTarifiId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_IsTarifiEkleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsTarifiEkleri_IsTarifleri_IsTarifiId",
                        column: x => x.IsTarifiId,
                        principalSchema: "catalog",
                        principalTable: "IsTarifleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsleri_IsTarifiId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "IsTarifiId");

            migrationBuilder.CreateIndex(
                name: "IX_IsTarifiEkleri_IsTarifiId",
                schema: "catalog",
                table: "IsTarifiEkleri",
                column: "IsTarifiId");

            migrationBuilder.CreateIndex(
                name: "IX_IsTarifleri_SistemId",
                schema: "catalog",
                table: "IsTarifleri",
                column: "SistemId");

            migrationBuilder.AddForeignKey(
                name: "FK_FirmaIsleri_IsTarifleri_IsTarifiId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "IsTarifiId",
                principalSchema: "catalog",
                principalTable: "IsTarifleri",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirmaIsleri_IsTarifleri_IsTarifiId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropTable(
                name: "IsTarifiEkleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "IsTarifleri",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_FirmaIsleri_IsTarifiId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "IsTarifiId",
                schema: "catalog",
                table: "FirmaIsleri");
        }
    }
}
