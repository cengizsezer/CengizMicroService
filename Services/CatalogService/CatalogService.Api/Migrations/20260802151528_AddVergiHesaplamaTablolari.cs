using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVergiHesaplamaTablolari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VergiHesaplamalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    DonemYil = table.Column<short>(type: "smallint", nullable: false),
                    TicariKar = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    KvOrani = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 25.00m),
                    IndirimliOran = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    IndirimliOranMatrahi = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    AsgariKvHesapla = table.Column<bool>(type: "bit", nullable: false),
                    Notlar = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GuncellemeT = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VergiHesaplamalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VergiHesaplamalar_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VergiKalemleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Grup = table.Column<byte>(type: "tinyint", nullable: false),
                    AltGrup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KanunMaddesi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Hatirlatma = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OranBilgisi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UstSinirTuru = table.Column<byte>(type: "tinyint", nullable: true),
                    UstSinirDeger = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    DevredebilirMi = table.Column<bool>(type: "bit", nullable: false),
                    IstisnayaIliskinMi = table.Column<bool>(type: "bit", nullable: false),
                    BagliIstisnaKalemiId = table.Column<int>(type: "int", nullable: true),
                    AsgariMatrahtanDuser = table.Column<bool>(type: "bit", nullable: false),
                    MukellefiyetTuru = table.Column<byte>(type: "tinyint", nullable: false),
                    SiraNo = table.Column<short>(type: "smallint", nullable: false),
                    SistemKalemi = table.Column<bool>(type: "bit", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VergiKalemleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VergiKalemleri_VergiKalemleri_BagliIstisnaKalemiId",
                        column: x => x.BagliIstisnaKalemiId,
                        principalTable: "VergiKalemleri",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GecmisYilZararlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HesaplamaId = table.Column<int>(type: "int", nullable: false),
                    ZararYili = table.Column<short>(type: "smallint", nullable: false),
                    ZararTutari = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    MahsupEdilen = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GecmisYilZararlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GecmisYilZararlari_VergiHesaplamalar_HesaplamaId",
                        column: x => x.HesaplamaId,
                        principalTable: "VergiHesaplamalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VergiHesaplamaSatirlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HesaplamaId = table.Column<int>(type: "int", nullable: false),
                    VergiKalemiId = table.Column<int>(type: "int", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: false),
                    OncekiDonem = table.Column<decimal>(type: "decimal(19,2)", precision: 19, scale: 2, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VergiHesaplamaSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VergiHesaplamaSatirlari_VergiHesaplamalar_HesaplamaId",
                        column: x => x.HesaplamaId,
                        principalTable: "VergiHesaplamalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VergiHesaplamaSatirlari_VergiKalemleri_VergiKalemiId",
                        column: x => x.VergiKalemiId,
                        principalTable: "VergiKalemleri",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GecmisYilZararlari_HesaplamaId_ZararYili",
                table: "GecmisYilZararlari",
                columns: new[] { "HesaplamaId", "ZararYili" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VergiHesaplamalar_FirmaId_DonemYil",
                table: "VergiHesaplamalar",
                columns: new[] { "FirmaId", "DonemYil" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VergiHesaplamaSatirlari_HesaplamaId_VergiKalemiId",
                table: "VergiHesaplamaSatirlari",
                columns: new[] { "HesaplamaId", "VergiKalemiId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VergiHesaplamaSatirlari_VergiKalemiId",
                table: "VergiHesaplamaSatirlari",
                column: "VergiKalemiId");

            migrationBuilder.CreateIndex(
                name: "IX_VergiKalemleri_BagliIstisnaKalemiId",
                table: "VergiKalemleri",
                column: "BagliIstisnaKalemiId");

            migrationBuilder.CreateIndex(
                name: "IX_VergiKalemleri_Grup_SiraNo",
                table: "VergiKalemleri",
                columns: new[] { "Grup", "SiraNo" });

            migrationBuilder.CreateIndex(
                name: "IX_VergiKalemleri_Kod",
                table: "VergiKalemleri",
                column: "Kod",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GecmisYilZararlari");

            migrationBuilder.DropTable(
                name: "VergiHesaplamaSatirlari");

            migrationBuilder.DropTable(
                name: "VergiHesaplamalar");

            migrationBuilder.DropTable(
                name: "VergiKalemleri");
        }
    }
}
