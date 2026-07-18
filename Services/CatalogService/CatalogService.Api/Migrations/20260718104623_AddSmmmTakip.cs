using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSmmmTakip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SmmmKonular",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UstKonuId = table.Column<int>(type: "int", nullable: true),
                    Baslik = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IcerikMd = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    GuncellenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmmmKonular", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmmmKonular_SmmmKonular_UstKonuId",
                        column: x => x.UstKonuId,
                        principalSchema: "catalog",
                        principalTable: "SmmmKonular",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmmmHadler",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KonuId = table.Column<int>(type: "int", nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Birim = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Dayanak = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmmmHadler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmmmHadler_SmmmKonular_KonuId",
                        column: x => x.KonuId,
                        principalSchema: "catalog",
                        principalTable: "SmmmKonular",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SmmmHadDegerleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HadId = table.Column<int>(type: "int", nullable: false),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    Deger = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Teblig = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Not = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GuncellenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmmmHadDegerleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmmmHadDegerleri_SmmmHadler_HadId",
                        column: x => x.HadId,
                        principalSchema: "catalog",
                        principalTable: "SmmmHadler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SmmmHadDegerleri_HadId_Yil",
                schema: "catalog",
                table: "SmmmHadDegerleri",
                columns: new[] { "HadId", "Yil" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmmmHadler_Kod",
                schema: "catalog",
                table: "SmmmHadler",
                column: "Kod",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmmmHadler_KonuId",
                schema: "catalog",
                table: "SmmmHadler",
                column: "KonuId");

            migrationBuilder.CreateIndex(
                name: "IX_SmmmKonular_Slug",
                schema: "catalog",
                table: "SmmmKonular",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SmmmKonular_UstKonuId",
                schema: "catalog",
                table: "SmmmKonular",
                column: "UstKonuId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SmmmHadDegerleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "SmmmHadler",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "SmmmKonular",
                schema: "catalog");
        }
    }
}
