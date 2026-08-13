using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMizanNotuSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotAlacak",
                table: "MizanNotlari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotBakiye",
                table: "MizanNotlari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotBorc",
                table: "MizanNotlari",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotTarihi",
                table: "MizanNotlari",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotAlacak",
                table: "MizanNotlari");

            migrationBuilder.DropColumn(
                name: "SnapshotBakiye",
                table: "MizanNotlari");

            migrationBuilder.DropColumn(
                name: "SnapshotBorc",
                table: "MizanNotlari");

            migrationBuilder.DropColumn(
                name: "SnapshotTarihi",
                table: "MizanNotlari");
        }
    }
}
