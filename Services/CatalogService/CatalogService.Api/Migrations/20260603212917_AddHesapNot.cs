using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHesapNot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HesapNotlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HesapId = table.Column<int>(type: "int", nullable: false),
                    Kapsam = table.Column<int>(type: "int", nullable: false),
                    Tarih = table.Column<DateTime>(type: "date", nullable: true),
                    Yil = table.Column<int>(type: "int", nullable: true),
                    Ay = table.Column<int>(type: "int", nullable: true),
                    Metin = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Sabit = table.Column<bool>(type: "bit", nullable: false),
                    OlusturanKullanici = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OlusturmaZamani = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HesapNotlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HesapNotlari_Hesaplar_HesapId",
                        column: x => x.HesapId,
                        principalTable: "Hesaplar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HesapNotlari_HesapId",
                table: "HesapNotlari",
                column: "HesapId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HesapNotlari");
        }
    }
}
