using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDuzenleyenler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski tek-kayıt düzenleyen ayarları (kdv.duzenleyen.*) artık kullanılmıyor;
            // bilgi Duzenleyenler tablosuna taşındı. Stale veriyi temizle.
            migrationBuilder.Sql(
                "DELETE FROM [AppSettings] WHERE [Key] LIKE 'kdv.duzenleyen.%';");

            migrationBuilder.AddColumn<int>(
                name: "DuzenleyenId",
                table: "Firmalar",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Duzenleyenler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kisaltma = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Vkn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Soyadi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Adi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TicaretSicilNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Eposta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AlanKodu = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: true),
                    TelNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Duzenleyenler", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Firmalar_DuzenleyenId",
                table: "Firmalar",
                column: "DuzenleyenId");

            migrationBuilder.CreateIndex(
                name: "IX_Duzenleyenler_Aktif",
                table: "Duzenleyenler",
                column: "Aktif");

            migrationBuilder.AddForeignKey(
                name: "FK_Firmalar_Duzenleyenler_DuzenleyenId",
                table: "Firmalar",
                column: "DuzenleyenId",
                principalTable: "Duzenleyenler",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Firmalar_Duzenleyenler_DuzenleyenId",
                table: "Firmalar");

            migrationBuilder.DropTable(
                name: "Duzenleyenler");

            migrationBuilder.DropIndex(
                name: "IX_Firmalar_DuzenleyenId",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "DuzenleyenId",
                table: "Firmalar");
        }
    }
}
