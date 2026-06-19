using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicaretSicilModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicaretSicilIslemler",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kategori = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicaretSicilIslemler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicaretSicilAdimlar",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IslemId = table.Column<int>(type: "int", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Not = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicaretSicilAdimlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicaretSicilAdimlar_TicaretSicilIslemler_IslemId",
                        column: x => x.IslemId,
                        principalSchema: "catalog",
                        principalTable: "TicaretSicilIslemler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicaretSicilEkler",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdimId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tur = table.Column<int>(type: "int", nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, defaultValue: "application/pdf"),
                    FileId = table.Column<int>(type: "int", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicaretSicilEkler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicaretSicilEkler_TicaretSicilAdimlar_AdimId",
                        column: x => x.AdimId,
                        principalSchema: "catalog",
                        principalTable: "TicaretSicilAdimlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicaretSicilAdimlar_IslemId_Sira",
                schema: "catalog",
                table: "TicaretSicilAdimlar",
                columns: new[] { "IslemId", "Sira" });

            migrationBuilder.CreateIndex(
                name: "IX_TicaretSicilEkler_AdimId",
                schema: "catalog",
                table: "TicaretSicilEkler",
                column: "AdimId");

            migrationBuilder.CreateIndex(
                name: "IX_TicaretSicilIslemler_Slug",
                schema: "catalog",
                table: "TicaretSicilIslemler",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicaretSicilEkler",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TicaretSicilAdimlar",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "TicaretSicilIslemler",
                schema: "catalog");
        }
    }
}
