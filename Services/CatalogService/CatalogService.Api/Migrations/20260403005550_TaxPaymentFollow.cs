using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class TaxPaymentFollow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "RetiredUnemploymentEmployeeRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "RetiredSgkEmployeeRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.CreateTable(
                name: "TaxPayments",
                schema: "pkf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TahakkukNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TaxNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxpayerName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TaxType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPayments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxPayments_TahakkukNo_TaxNumber_TaxType",
                schema: "pkf",
                table: "TaxPayments",
                columns: new[] { "TahakkukNo", "TaxNumber", "TaxType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaxPayments",
                schema: "pkf");

            migrationBuilder.AlterColumn<decimal>(
                name: "RetiredUnemploymentEmployeeRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "RetiredSgkEmployeeRate",
                schema: "pkf",
                table: "PayrollParameters",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }
    }
}
