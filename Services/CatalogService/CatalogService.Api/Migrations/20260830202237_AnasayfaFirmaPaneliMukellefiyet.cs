using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AnasayfaFirmaPaneliMukellefiyet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EDefter",
                schema: "catalog",
                table: "FirmaSicilBilgileri",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EFatura",
                schema: "catalog",
                table: "FirmaSicilBilgileri",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IseBaslamaTarihi",
                schema: "catalog",
                table: "FirmaSicilBilgileri",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MukellefiyetTurleri",
                schema: "catalog",
                table: "FirmaSicilBilgileri",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EDefter",
                schema: "catalog",
                table: "FirmaSicilBilgileri");

            migrationBuilder.DropColumn(
                name: "EFatura",
                schema: "catalog",
                table: "FirmaSicilBilgileri");

            migrationBuilder.DropColumn(
                name: "IseBaslamaTarihi",
                schema: "catalog",
                table: "FirmaSicilBilgileri");

            migrationBuilder.DropColumn(
                name: "MukellefiyetTurleri",
                schema: "catalog",
                table: "FirmaSicilBilgileri");
        }
    }
}
