using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFinansFieldsToMukellef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AcikBakiye",
                table: "Mukellefler",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinansAciklama",
                table: "Mukellefler",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FinansSinifi",
                table: "Mukellefler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SonOdemeTarihi",
                table: "Mukellefler",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mukellefler_FinansSinifi",
                table: "Mukellefler",
                column: "FinansSinifi");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Mukellefler_FinansSinifi",
                table: "Mukellefler");

            migrationBuilder.DropColumn(
                name: "AcikBakiye",
                table: "Mukellefler");

            migrationBuilder.DropColumn(
                name: "FinansAciklama",
                table: "Mukellefler");

            migrationBuilder.DropColumn(
                name: "FinansSinifi",
                table: "Mukellefler");

            migrationBuilder.DropColumn(
                name: "SonOdemeTarihi",
                table: "Mukellefler");
        }
    }
}
