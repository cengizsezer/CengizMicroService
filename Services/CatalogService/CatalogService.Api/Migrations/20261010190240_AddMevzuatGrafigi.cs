using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMevzuatGrafigi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MevzuatKartlar",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Madde = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Ozet = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MevzuatKartlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MevzuatTuvaller",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MevzuatTuvaller", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MevzuatBaglantilar",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KaynakKartId = table.Column<int>(type: "int", nullable: false),
                    HedefKartId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MevzuatBaglantilar", x => x.Id);
                    table.CheckConstraint("CK_MevzuatBaglantilar_KendineDegil", "[KaynakKartId] <> [HedefKartId]");
                    table.ForeignKey(
                        name: "FK_MevzuatBaglantilar_MevzuatKartlar_HedefKartId",
                        column: x => x.HedefKartId,
                        principalSchema: "catalog",
                        principalTable: "MevzuatKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MevzuatBaglantilar_MevzuatKartlar_KaynakKartId",
                        column: x => x.KaynakKartId,
                        principalSchema: "catalog",
                        principalTable: "MevzuatKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MevzuatDislamalar",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AKartId = table.Column<int>(type: "int", nullable: false),
                    BKartId = table.Column<int>(type: "int", nullable: false),
                    Not = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MevzuatDislamalar", x => x.Id);
                    table.CheckConstraint("CK_MevzuatDislamalar_Sirali", "[AKartId] < [BKartId]");
                    table.ForeignKey(
                        name: "FK_MevzuatDislamalar_MevzuatKartlar_AKartId",
                        column: x => x.AKartId,
                        principalSchema: "catalog",
                        principalTable: "MevzuatKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MevzuatDislamalar_MevzuatKartlar_BKartId",
                        column: x => x.BKartId,
                        principalSchema: "catalog",
                        principalTable: "MevzuatKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MevzuatSartTakipleri",
                schema: "catalog",
                columns: table => new
                {
                    FirmaKartId = table.Column<int>(type: "int", nullable: false),
                    SartKartId = table.Column<int>(type: "int", nullable: false),
                    Tamamlandi = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MevzuatSartTakipleri", x => new { x.FirmaKartId, x.SartKartId });
                    table.ForeignKey(
                        name: "FK_MevzuatSartTakipleri_MevzuatKartlar_FirmaKartId",
                        column: x => x.FirmaKartId,
                        principalSchema: "catalog",
                        principalTable: "MevzuatKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MevzuatSartTakipleri_MevzuatKartlar_SartKartId",
                        column: x => x.SartKartId,
                        principalSchema: "catalog",
                        principalTable: "MevzuatKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MevzuatTuvalKartlari",
                schema: "catalog",
                columns: table => new
                {
                    TuvalId = table.Column<int>(type: "int", nullable: false),
                    KartId = table.Column<int>(type: "int", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MevzuatTuvalKartlari", x => new { x.TuvalId, x.KartId });
                    table.ForeignKey(
                        name: "FK_MevzuatTuvalKartlari_MevzuatKartlar_KartId",
                        column: x => x.KartId,
                        principalSchema: "catalog",
                        principalTable: "MevzuatKartlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MevzuatTuvalKartlari_MevzuatTuvaller_TuvalId",
                        column: x => x.TuvalId,
                        principalSchema: "catalog",
                        principalTable: "MevzuatTuvaller",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MevzuatBaglantilar_HedefKartId",
                schema: "catalog",
                table: "MevzuatBaglantilar",
                column: "HedefKartId");

            migrationBuilder.CreateIndex(
                name: "IX_MevzuatBaglantilar_KaynakKartId_HedefKartId",
                schema: "catalog",
                table: "MevzuatBaglantilar",
                columns: new[] { "KaynakKartId", "HedefKartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MevzuatDislamalar_AKartId_BKartId",
                schema: "catalog",
                table: "MevzuatDislamalar",
                columns: new[] { "AKartId", "BKartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MevzuatDislamalar_BKartId",
                schema: "catalog",
                table: "MevzuatDislamalar",
                column: "BKartId");

            migrationBuilder.CreateIndex(
                name: "IX_MevzuatKartlar_Tur",
                schema: "catalog",
                table: "MevzuatKartlar",
                column: "Tur");

            migrationBuilder.CreateIndex(
                name: "IX_MevzuatSartTakipleri_SartKartId",
                schema: "catalog",
                table: "MevzuatSartTakipleri",
                column: "SartKartId");

            migrationBuilder.CreateIndex(
                name: "IX_MevzuatTuvalKartlari_KartId",
                schema: "catalog",
                table: "MevzuatTuvalKartlari",
                column: "KartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MevzuatBaglantilar",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MevzuatDislamalar",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MevzuatSartTakipleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MevzuatTuvalKartlari",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MevzuatKartlar",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MevzuatTuvaller",
                schema: "catalog");
        }
    }
}
