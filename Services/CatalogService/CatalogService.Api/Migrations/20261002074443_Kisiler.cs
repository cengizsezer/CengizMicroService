using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class Kisiler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KisiId",
                schema: "catalog",
                table: "FirmaIsiAlicilari",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Kisiler",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Eposta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kisiler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Kisiler_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsiAlicilari_KisiId",
                schema: "catalog",
                table: "FirmaIsiAlicilari",
                column: "KisiId");

            migrationBuilder.CreateIndex(
                name: "IX_Kisiler_FirmaId_Eposta",
                schema: "catalog",
                table: "Kisiler",
                columns: new[] { "FirmaId", "Eposta" },
                unique: true,
                filter: "[Eposta] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FirmaIsiAlicilari_Kisiler_KisiId",
                schema: "catalog",
                table: "FirmaIsiAlicilari",
                column: "KisiId",
                principalSchema: "catalog",
                principalTable: "Kisiler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirmaIsiAlicilari_Kisiler_KisiId",
                schema: "catalog",
                table: "FirmaIsiAlicilari");

            migrationBuilder.DropTable(
                name: "Kisiler",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_FirmaIsiAlicilari_KisiId",
                schema: "catalog",
                table: "FirmaIsiAlicilari");

            migrationBuilder.DropColumn(
                name: "KisiId",
                schema: "catalog",
                table: "FirmaIsiAlicilari");
        }
    }
}
