using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class EtiketSistemiTanim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EtiketBoyutlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kapsam = table.Column<byte>(type: "tinyint", nullable: false),
                    FirmaId = table.Column<int>(type: "int", nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtiketBoyutlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EtiketBoyutlari_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EtiketDegerleri",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BoyutId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Renk = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtiketDegerleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EtiketDegerleri_EtiketBoyutlari_BoyutId",
                        column: x => x.BoyutId,
                        principalTable: "EtiketBoyutlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EtiketKurallari",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BoyutId = table.Column<int>(type: "int", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    EslesmeTipi = table.Column<byte>(type: "tinyint", nullable: false),
                    Desen = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DegerId = table.Column<int>(type: "int", nullable: false),
                    FirmaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtiketKurallari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EtiketKurallari_EtiketBoyutlari_BoyutId",
                        column: x => x.BoyutId,
                        principalTable: "EtiketBoyutlari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EtiketKurallari_EtiketDegerleri_DegerId",
                        column: x => x.DegerId,
                        principalTable: "EtiketDegerleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EtiketBoyutlari_FirmaId_Sira",
                table: "EtiketBoyutlari",
                columns: new[] { "FirmaId", "Sira" });

            migrationBuilder.CreateIndex(
                name: "IX_EtiketDegerleri_BoyutId_Ad",
                table: "EtiketDegerleri",
                columns: new[] { "BoyutId", "Ad" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EtiketDegerleri_BoyutId_Sira",
                table: "EtiketDegerleri",
                columns: new[] { "BoyutId", "Sira" });

            migrationBuilder.CreateIndex(
                name: "IX_EtiketKurallari_BoyutId_Sira",
                table: "EtiketKurallari",
                columns: new[] { "BoyutId", "Sira" });

            migrationBuilder.CreateIndex(
                name: "IX_EtiketKurallari_DegerId",
                table: "EtiketKurallari",
                column: "DegerId");

            migrationBuilder.CreateIndex(
                name: "IX_EtiketKurallari_FirmaId",
                table: "EtiketKurallari",
                column: "FirmaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EtiketKurallari");

            migrationBuilder.DropTable(
                name: "EtiketDegerleri");

            migrationBuilder.DropTable(
                name: "EtiketBoyutlari");
        }
    }
}
