using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMizanNotuTuru : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NotTuru",
                table: "MizanNotlari",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotTuru",
                table: "MizanNotlari");
        }
    }
}
