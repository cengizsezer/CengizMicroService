using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeedHistories",
                schema: "pkf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SeedKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeedHistories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeedHistories_SeedKey",
                schema: "pkf",
                table: "SeedHistories",
                column: "SeedKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeedHistories",
                schema: "pkf");
        }
    }
}
