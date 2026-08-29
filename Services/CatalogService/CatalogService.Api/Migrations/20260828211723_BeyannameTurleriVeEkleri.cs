using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class BeyannameTurleriVeEkleri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BeyannameEkleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeclarationId = table.Column<int>(type: "int", nullable: false),
                    Tur = table.Column<byte>(type: "tinyint", nullable: false),
                    FileId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    YukleyenKullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeyannameEkleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeyannameEkleri_Declarations_DeclarationId",
                        column: x => x.DeclarationId,
                        principalSchema: "catalog",
                        principalTable: "Declarations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeyannameTurleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Deger = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kod = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeyannameTurleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeyannameEkleri_DeclarationId_Tur",
                schema: "catalog",
                table: "BeyannameEkleri",
                columns: new[] { "DeclarationId", "Tur" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeyannameTurleri_Deger",
                schema: "catalog",
                table: "BeyannameTurleri",
                column: "Deger",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeyannameEkleri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "BeyannameTurleri",
                schema: "catalog");
        }
    }
}
