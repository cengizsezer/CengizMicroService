using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IdentityService.Migrations
{
    /// <inheritdoc />
    public partial class AjanKimligi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ajanlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AnahtarHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    AnahtarOnEki = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    OlusturanKullaniciId = table.Column<int>(type: "int", nullable: false),
                    OlusturmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SonKullanim = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GecerlilikBitisi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    IptalZamani = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IptalNedeni = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ajanlar", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ajanlar_AnahtarOnEki",
                table: "Ajanlar",
                column: "AnahtarOnEki");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ajanlar");
        }
    }
}
