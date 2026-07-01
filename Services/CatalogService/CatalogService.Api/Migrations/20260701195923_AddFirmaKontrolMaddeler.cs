using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFirmaKontrolMaddeler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirmaKontrolMaddeler",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirmaId = table.Column<int>(type: "int", nullable: false),
                    MaddeKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsCustom = table.Column<bool>(type: "bit", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    SoruMetni = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsChecked = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Not = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SiraNo = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirmaKontrolMaddeler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirmaKontrolMaddeler_Firmalar_FirmaId",
                        column: x => x.FirmaId,
                        principalTable: "Firmalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirmaKontrolMaddeler_FirmaId",
                table: "FirmaKontrolMaddeler",
                column: "FirmaId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaKontrolMaddeler_FirmaId_MaddeKey",
                table: "FirmaKontrolMaddeler",
                columns: new[] { "FirmaId", "MaddeKey" },
                unique: true,
                filter: "[MaddeKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirmaKontrolMaddeler");
        }
    }
}
