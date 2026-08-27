using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFinansmanKisitlamaOranlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinansmanKisitlamaOranlari",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    Oran = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Dayanak = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Not = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    GuncellenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinansmanKisitlamaOranlari", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinansmanKisitlamaOranlari_Yil",
                schema: "catalog",
                table: "FinansmanKisitlamaOranlari",
                column: "Yil",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinansmanKisitlamaOranlari",
                schema: "catalog");
        }
    }
}
