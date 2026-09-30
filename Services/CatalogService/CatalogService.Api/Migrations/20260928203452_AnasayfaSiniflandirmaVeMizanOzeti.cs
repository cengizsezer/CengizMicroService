using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AnasayfaSiniflandirmaVeMizanOzeti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "DefterUsulu",
                table: "Firmalar",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "DefterUsuluKaynagi",
                table: "Firmalar",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "HesapDonemi",
                table: "Firmalar",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MizanFormati",
                table: "Firmalar",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OzelDonemBas",
                table: "Firmalar",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OzelDonemBit",
                table: "Firmalar",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "VergiTuru",
                table: "Firmalar",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "VergiTuruKaynagi",
                table: "Firmalar",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<decimal>(
                name: "AlacakToplam",
                table: "FirmaKontrolMizanAgaclari",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BorcToplam",
                table: "FirmaKontrolMizanAgaclari",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeviyeSayisi",
                table: "FirmaKontrolMizanAgaclari",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefterUsulu",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "DefterUsuluKaynagi",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "HesapDonemi",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "MizanFormati",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "OzelDonemBas",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "OzelDonemBit",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "VergiTuru",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "VergiTuruKaynagi",
                table: "Firmalar");

            migrationBuilder.DropColumn(
                name: "AlacakToplam",
                table: "FirmaKontrolMizanAgaclari");

            migrationBuilder.DropColumn(
                name: "BorcToplam",
                table: "FirmaKontrolMizanAgaclari");

            migrationBuilder.DropColumn(
                name: "SeviyeSayisi",
                table: "FirmaKontrolMizanAgaclari");
        }
    }
}
