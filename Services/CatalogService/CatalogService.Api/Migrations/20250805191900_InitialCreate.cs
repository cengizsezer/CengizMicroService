using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                schema: "catalog",
                table: "AccountingCodes");

            migrationBuilder.RenameColumn(
                name: "PersonnelCode",
                schema: "catalog",
                table: "Personnels",
                newName: "LastName");

            migrationBuilder.RenameColumn(
                name: "FullName",
                schema: "catalog",
                table: "Personnels",
                newName: "Unit");

            migrationBuilder.AddColumn<string>(
                name: "Company",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExpenseCenter",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IBAN",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(26)",
                maxLength: 26,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NationalId",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "catalog",
                table: "AccountingCodes",
                type: "nvarchar(25)",
                maxLength: 25,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "catalog",
                table: "AccountingCodes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Company",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "Email",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "ExpenseCenter",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "IBAN",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "NationalId",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "Title",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "catalog",
                table: "AccountingCodes");

            migrationBuilder.RenameColumn(
                name: "Unit",
                schema: "catalog",
                table: "Personnels",
                newName: "FullName");

            migrationBuilder.RenameColumn(
                name: "LastName",
                schema: "catalog",
                table: "Personnels",
                newName: "PersonnelCode");

            migrationBuilder.AlterColumn<string>(
                name: "Code",
                schema: "catalog",
                table: "AccountingCodes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(25)",
                oldMaxLength: 25);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "catalog",
                table: "AccountingCodes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
