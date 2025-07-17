using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "VatRate",
                schema: "catalog",
                table: "ReceiptItems",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "AccountingCode",
                schema: "catalog",
                table: "ReceiptItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountExclVat",
                schema: "catalog",
                table: "ReceiptItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Company",
                schema: "catalog",
                table: "ReceiptItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                schema: "catalog",
                table: "ReceiptItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "catalog",
                table: "ReceiptItems",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PersonnelCode",
                schema: "catalog",
                table: "ReceiptItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "VatRate",
                schema: "catalog",
                table: "ProductDetails",
                type: "decimal(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "AccountingCode",
                schema: "catalog",
                table: "ProductDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountExclVat",
                schema: "catalog",
                table: "ProductDetails",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Company",
                schema: "catalog",
                table: "ProductDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                schema: "catalog",
                table: "ProductDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "catalog",
                table: "ProductDetails",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PersonnelCode",
                schema: "catalog",
                table: "ProductDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AccountingCode",
                schema: "catalog",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "AmountExclVat",
                schema: "catalog",
                table: "Expenses",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                schema: "catalog",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                schema: "catalog",
                table: "Expenses",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PersonnelCode",
                schema: "catalog",
                table: "Expenses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "VatRate",
                schema: "catalog",
                table: "Expenses",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountingCode",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "AmountExclVat",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "Company",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "FullName",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "PersonnelCode",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "AccountingCode",
                schema: "catalog",
                table: "ProductDetails");

            migrationBuilder.DropColumn(
                name: "AmountExclVat",
                schema: "catalog",
                table: "ProductDetails");

            migrationBuilder.DropColumn(
                name: "Company",
                schema: "catalog",
                table: "ProductDetails");

            migrationBuilder.DropColumn(
                name: "FullName",
                schema: "catalog",
                table: "ProductDetails");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "catalog",
                table: "ProductDetails");

            migrationBuilder.DropColumn(
                name: "PersonnelCode",
                schema: "catalog",
                table: "ProductDetails");

            migrationBuilder.DropColumn(
                name: "AccountingCode",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "AmountExclVat",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "FullName",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "Note",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PersonnelCode",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "VatRate",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.AlterColumn<decimal>(
                name: "VatRate",
                schema: "catalog",
                table: "ReceiptItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "VatRate",
                schema: "catalog",
                table: "ProductDetails",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)");
        }
    }
}
