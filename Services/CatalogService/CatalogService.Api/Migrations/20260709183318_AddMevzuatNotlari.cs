using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMevzuatNotlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MevzuatNotlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kategori = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    MaddeNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Baslik = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ozet = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Icerik = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Etiketler = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Kaynak = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MevzuatNotlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MevzuatNotlari_Kategori",
                schema: "catalog",
                table: "MevzuatNotlari",
                column: "Kategori");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MevzuatNotlari",
                schema: "catalog");
        }
    }
}
