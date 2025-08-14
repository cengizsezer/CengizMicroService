using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "ReceiptItems",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "ProductDetails",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "Personnels",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "Expenses",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantNo",
                schema: "catalog",
                table: "AccountingCodes",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_TenantNo_ExpenseId",
                schema: "catalog",
                table: "ReceiptItems",
                columns: new[] { "TenantNo", "ExpenseId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_TenantNo_ReceiptDate",
                schema: "catalog",
                table: "ReceiptItems",
                columns: new[] { "TenantNo", "ReceiptDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetails_TenantNo_ReceiptItemId",
                schema: "catalog",
                table: "ProductDetails",
                columns: new[] { "TenantNo", "ReceiptItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Personnels_TenantNo_FullName",
                schema: "catalog",
                table: "Personnels",
                columns: new[] { "TenantNo", "FullName" });

            migrationBuilder.CreateIndex(
                name: "IX_Personnels_TenantNo_NationalId",
                schema: "catalog",
                table: "Personnels",
                columns: new[] { "TenantNo", "NationalId" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantNo_ExpenseCode",
                schema: "catalog",
                table: "Expenses",
                columns: new[] { "TenantNo", "ExpenseCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantNo_ExpenseDate",
                schema: "catalog",
                table: "Expenses",
                columns: new[] { "TenantNo", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_TenantNo_PersonnelAccountingCode_ExpenseDate",
                schema: "catalog",
                table: "Expenses",
                columns: new[] { "TenantNo", "PersonnelAccountingCode", "ExpenseDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingCodes_TenantNo_Code",
                schema: "catalog",
                table: "AccountingCodes",
                columns: new[] { "TenantNo", "Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReceiptItems_TenantNo_ExpenseId",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptItems_TenantNo_ReceiptDate",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_ProductDetails_TenantNo_ReceiptItemId",
                schema: "catalog",
                table: "ProductDetails");

            migrationBuilder.DropIndex(
                name: "IX_Personnels_TenantNo_FullName",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropIndex(
                name: "IX_Personnels_TenantNo_NationalId",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_TenantNo_ExpenseCode",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_TenantNo_ExpenseDate",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_TenantNo_PersonnelAccountingCode_ExpenseDate",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_AccountingCodes_TenantNo_Code",
                schema: "catalog",
                table: "AccountingCodes");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "ReceiptItems");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "ProductDetails");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "Personnels");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "TenantNo",
                schema: "catalog",
                table: "AccountingCodes");
        }
    }
}
