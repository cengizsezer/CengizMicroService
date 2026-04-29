using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovos.InvoiceWorker.Data.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SovosCompanies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CompanyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EncryptedPassword = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    NotificationEmail = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastSuccessfulRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastFailedRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SovosCompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SovosInvoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FaturaNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GondericiVkn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirmaUnvani = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FaturaTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ToplamVergi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IskontoTutari = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Artirim = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SiparisNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SonOdemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DuzenlenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OlusturulmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SovosInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SovosInvoices_SovosCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "SovosCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SovosInvoices_CompanyId_FaturaNo_GondericiVkn",
                table: "SovosInvoices",
                columns: new[] { "CompanyId", "FaturaNo", "GondericiVkn" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SovosInvoices");

            migrationBuilder.DropTable(
                name: "SovosCompanies");
        }
    }
}
