using System;
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
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "AccountingCodes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PersonnelFullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PersonnelAccountingCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpenseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Personnels",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NormalExpenseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SalaryExpenseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CaseExpenseNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IBAN = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpenseCenter = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Personnels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Plate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Driver = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description1 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description2 = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Gear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fuel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Fleet = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptItems",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpenseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Hizmet"),
                    AccountingCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AccountingCodeDescription = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Adet"),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVat = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpenseId = table.Column<int>(type: "int", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptItems_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalSchema: "catalog",
                        principalTable: "Expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductDetails",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    TaxBase = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VatRate = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReceiptItemId = table.Column<int>(type: "int", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductDetails_ReceiptItems_ReceiptItemId",
                        column: x => x.ReceiptItemId,
                        principalSchema: "catalog",
                        principalTable: "ReceiptItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingCodes_TenantNo_Code",
                schema: "catalog",
                table: "AccountingCodes",
                columns: new[] { "TenantNo", "Code" });

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
                name: "IX_ProductDetails_ReceiptItemId",
                schema: "catalog",
                table: "ProductDetails",
                column: "ReceiptItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductDetails_TenantNo_ReceiptItemId",
                schema: "catalog",
                table: "ProductDetails",
                columns: new[] { "TenantNo", "ReceiptItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptItems_ExpenseId",
                schema: "catalog",
                table: "ReceiptItems",
                column: "ExpenseId");

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
                name: "IX_Vehicles_Plate",
                table: "Vehicles",
                column: "Plate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingCodes",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Personnels",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "ProductDetails",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "ReceiptItems",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Expenses",
                schema: "catalog");
        }
    }
}
