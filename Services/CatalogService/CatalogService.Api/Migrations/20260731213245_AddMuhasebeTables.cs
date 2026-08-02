using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMuhasebeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Fisler",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonemYil = table.Column<short>(type: "smallint", nullable: false),
                    FisNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tarih = table.Column<DateTime>(type: "date", nullable: false),
                    FisTuru = table.Column<byte>(type: "tinyint", nullable: false),
                    BelgeNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Kaynak = table.Column<byte>(type: "tinyint", nullable: false),
                    Durum = table.Column<byte>(type: "tinyint", nullable: false),
                    OlusturanId = table.Column<int>(type: "int", nullable: false),
                    OlusturmaT = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeT = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fisler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HesapPlani",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UstHesapId = table.Column<int>(type: "int", nullable: true),
                    Kod = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    KodDuz = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SegmentKod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Seviye = table.Column<byte>(type: "tinyint", nullable: false),
                    HesapTuru = table.Column<byte>(type: "tinyint", nullable: false),
                    Karakter = table.Column<byte>(type: "tinyint", nullable: false),
                    HareketGorur = table.Column<bool>(type: "bit", nullable: false),
                    SistemHesabi = table.Column<bool>(type: "bit", nullable: false),
                    ParaBirimi = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: true),
                    BankaKodu = table.Column<string>(type: "nchar(4)", fixedLength: true, maxLength: 4, nullable: true),
                    Iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    Yol = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HesapPlani", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HesapPlani_HesapPlani_UstHesapId",
                        column: x => x.UstHesapId,
                        principalSchema: "catalog",
                        principalTable: "HesapPlani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KodMaskeleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SegmentUzunluk = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ayrac = table.Column<string>(type: "nchar(1)", fixedLength: true, maxLength: 1, nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KodMaskeleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasrafMerkezleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasrafMerkezleri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FisSatirlar",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FisId = table.Column<int>(type: "int", nullable: false),
                    SiraNo = table.Column<short>(type: "smallint", nullable: false),
                    HesapId = table.Column<int>(type: "int", nullable: false),
                    MasrafMerkeziId = table.Column<int>(type: "int", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Borc = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    Alacak = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Doviz = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Kur = table.Column<decimal>(type: "decimal(19,6)", precision: 19, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FisSatirlar", x => x.Id);
                    table.CheckConstraint("CK_TekTaraf", "([Borc] > 0 AND [Alacak] = 0) OR ([Alacak] > 0 AND [Borc] = 0)");
                    table.ForeignKey(
                        name: "FK_FisSatirlar_Fisler_FisId",
                        column: x => x.FisId,
                        principalSchema: "catalog",
                        principalTable: "Fisler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FisSatirlar_HesapPlani_HesapId",
                        column: x => x.HesapId,
                        principalSchema: "catalog",
                        principalTable: "HesapPlani",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FisSatirlar_MasrafMerkezleri_MasrafMerkeziId",
                        column: x => x.MasrafMerkeziId,
                        principalSchema: "catalog",
                        principalTable: "MasrafMerkezleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_FisNo",
                schema: "catalog",
                table: "Fisler",
                columns: new[] { "TenantNo", "DonemYil", "FisNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FisSatir_Hesap",
                schema: "catalog",
                table: "FisSatirlar",
                columns: new[] { "HesapId", "FisId" });

            migrationBuilder.CreateIndex(
                name: "IX_FisSatirlar_FisId",
                schema: "catalog",
                table: "FisSatirlar",
                column: "FisId");

            migrationBuilder.CreateIndex(
                name: "IX_FisSatirlar_MasrafMerkeziId",
                schema: "catalog",
                table: "FisSatirlar",
                column: "MasrafMerkeziId");

            migrationBuilder.CreateIndex(
                name: "IX_HesapPlani_KodDuz",
                schema: "catalog",
                table: "HesapPlani",
                columns: new[] { "TenantNo", "KodDuz" });

            migrationBuilder.CreateIndex(
                name: "IX_HesapPlani_UstHesapId",
                schema: "catalog",
                table: "HesapPlani",
                column: "UstHesapId");

            migrationBuilder.CreateIndex(
                name: "IX_HesapPlani_Yol",
                schema: "catalog",
                table: "HesapPlani",
                columns: new[] { "TenantNo", "Yol" });

            migrationBuilder.CreateIndex(
                name: "UQ_HesapKod",
                schema: "catalog",
                table: "HesapPlani",
                columns: new[] { "TenantNo", "Kod" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KodMaskeleri_TenantNo",
                schema: "catalog",
                table: "KodMaskeleri",
                column: "TenantNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasrafMerkezleri_TenantNo_Kod",
                schema: "catalog",
                table: "MasrafMerkezleri",
                columns: new[] { "TenantNo", "Kod" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FisSatirlar",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "KodMaskeleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Fisler",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "HesapPlani",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "MasrafMerkezleri",
                schema: "catalog");
        }
    }
}
