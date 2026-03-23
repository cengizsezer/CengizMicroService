using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeclarationStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsApproved",
                schema: "catalog",
                table: "Declarations");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                schema: "catalog",
                table: "Declarations");

            migrationBuilder.AddColumn<int>(
                name: "DeclarationStatus",
                schema: "catalog",
                table: "Declarations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaymentStatus",
                schema: "catalog",
                table: "Declarations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeclarationStatus",
                schema: "catalog",
                table: "Declarations");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                schema: "catalog",
                table: "Declarations");

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                schema: "catalog",
                table: "Declarations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                schema: "catalog",
                table: "Declarations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
