using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChecklistSablonlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SablonId",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChecklistSablonlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistSablonlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistSablonMaddeleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SablonId = table.Column<int>(type: "int", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Baslik = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Tekrar = table.Column<byte>(type: "tinyint", nullable: false),
                    GunKurali = table.Column<byte>(type: "tinyint", nullable: false),
                    AyinGunu = table.Column<int>(type: "int", nullable: true),
                    KategoriId = table.Column<int>(type: "int", nullable: true),
                    IsTarifiId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistSablonMaddeleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistSablonMaddeleri_ChecklistSablonlari_SablonId",
                        column: x => x.SablonId,
                        principalSchema: "catalog",
                        principalTable: "ChecklistSablonlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChecklistSablonMaddeleri_IsKategorileri_KategoriId",
                        column: x => x.KategoriId,
                        principalSchema: "catalog",
                        principalTable: "IsKategorileri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChecklistSablonMaddeleri_IsTarifleri_IsTarifiId",
                        column: x => x.IsTarifiId,
                        principalSchema: "catalog",
                        principalTable: "IsTarifleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsleri_SablonId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "SablonId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistSablonlari_Ad",
                schema: "catalog",
                table: "ChecklistSablonlari",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistSablonMaddeleri_IsTarifiId",
                schema: "catalog",
                table: "ChecklistSablonMaddeleri",
                column: "IsTarifiId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistSablonMaddeleri_KategoriId",
                schema: "catalog",
                table: "ChecklistSablonMaddeleri",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistSablonMaddeleri_SablonId",
                schema: "catalog",
                table: "ChecklistSablonMaddeleri",
                column: "SablonId");

            migrationBuilder.AddForeignKey(
                name: "FK_FirmaIsleri_ChecklistSablonlari_SablonId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "SablonId",
                principalSchema: "catalog",
                principalTable: "ChecklistSablonlari",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirmaIsleri_ChecklistSablonlari_SablonId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropTable(
                name: "ChecklistSablonMaddeleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ChecklistSablonlari",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_FirmaIsleri_SablonId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "SablonId",
                schema: "catalog",
                table: "FirmaIsleri");
        }
    }
}
