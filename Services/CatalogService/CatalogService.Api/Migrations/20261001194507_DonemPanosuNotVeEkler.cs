using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class DonemPanosuNotVeEkler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Not",
                schema: "catalog",
                table: "IsTamamlamalari",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IsTamamlamaEkleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsTamamlamaId = table.Column<long>(type: "bigint", nullable: false),
                    FileId = table.Column<int>(type: "int", nullable: false),
                    DosyaAdi = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Boyut = table.Column<long>(type: "bigint", nullable: false),
                    YuklemeZamani = table.Column<DateTime>(type: "datetime2", nullable: false),
                    YukleyenKullaniciId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    YukleyenKullaniciAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsTamamlamaEkleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IsTamamlamaEkleri_IsTamamlamalari_IsTamamlamaId",
                        column: x => x.IsTamamlamaId,
                        principalSchema: "catalog",
                        principalTable: "IsTamamlamalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IsTamamlamaEkleri_IsTamamlamaId",
                schema: "catalog",
                table: "IsTamamlamaEkleri",
                column: "IsTamamlamaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IsTamamlamaEkleri",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "Not",
                schema: "catalog",
                table: "IsTamamlamalari");
        }
    }
}
