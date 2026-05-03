using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmaAndMukellef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Firmalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VergiKimlikNo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Unvan = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    KisaAd = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Telefon = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TicaretSicilNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    VergiDairesi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Firmalar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mukellefler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SozlesmeNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SozlesmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Unvan = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    VergiKimlikNo = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    IptalFeshTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Hizmet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Telefon = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mukellefler", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Firmalar_Aktif",
                table: "Firmalar",
                column: "Aktif");

            migrationBuilder.CreateIndex(
                name: "IX_Firmalar_VergiKimlikNo",
                table: "Firmalar",
                column: "VergiKimlikNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mukellefler_Durum",
                table: "Mukellefler",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_Mukellefler_SozlesmeNo",
                table: "Mukellefler",
                column: "SozlesmeNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mukellefler_Unvan",
                table: "Mukellefler",
                column: "Unvan");

            migrationBuilder.CreateIndex(
                name: "IX_Mukellefler_VergiKimlikNo",
                table: "Mukellefler",
                column: "VergiKimlikNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Firmalar");

            migrationBuilder.DropTable(
                name: "Mukellefler");
        }
    }
}
