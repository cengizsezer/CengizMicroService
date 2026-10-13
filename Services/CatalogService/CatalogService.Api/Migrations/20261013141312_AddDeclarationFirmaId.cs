using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDeclarationFirmaId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FirmaId",
                schema: "catalog",
                table: "Declarations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Declarations_FirmaId",
                schema: "catalog",
                table: "Declarations",
                column: "FirmaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Declarations_Firmalar_FirmaId",
                schema: "catalog",
                table: "Declarations",
                column: "FirmaId",
                principalTable: "Firmalar",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Mevcut beyannameleri Firmalarım'a bağla: eski CustomerCompanies kaydının VKN'si (TaxNumber) ile
            // dbo.Firmalar.VergiKimlikNo, baş/son boşluklar kırpılarak. Kurallar:
            //   • yalnız FirmaId'si boş satırlar güncellenir (yeniden çalışırsa dokunduğunu ezmez);
            //   • boş/NULL VKN hiçbir şeyle eşleşmez;
            //   • Firmalarım'da aynı VKN'yi taşıyan birden fazla firma varsa o VKN eşlenmez (yanlış firmaya
            //     bağlamaktansa "Eşleşmeyen firma"da kalsın).
            // Hiçbir satır silinmez; CustomerCompanyId, CompanyName ve dbo.CustomerCompanies olduğu gibi kalır.
            // Tablolar boşsa hiçbir şey yapmaz.
            migrationBuilder.Sql(@"
UPDATE d
SET d.FirmaId = e.FirmaId
FROM [catalog].[Declarations] d
INNER JOIN [dbo].[CustomerCompanies] c ON c.Id = d.CustomerCompanyId
INNER JOIN (
    SELECT LTRIM(RTRIM(f.VergiKimlikNo)) AS Vkn, MIN(f.Id) AS FirmaId
    FROM [dbo].[Firmalar] f
    WHERE f.VergiKimlikNo IS NOT NULL AND LTRIM(RTRIM(f.VergiKimlikNo)) <> N''
    GROUP BY LTRIM(RTRIM(f.VergiKimlikNo))
    HAVING COUNT(*) = 1
) e ON e.Vkn = LTRIM(RTRIM(c.TaxNumber))
WHERE d.FirmaId IS NULL
  AND c.TaxNumber IS NOT NULL
  AND LTRIM(RTRIM(c.TaxNumber)) <> N'';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Declarations_Firmalar_FirmaId",
                schema: "catalog",
                table: "Declarations");

            migrationBuilder.DropIndex(
                name: "IX_Declarations_FirmaId",
                schema: "catalog",
                table: "Declarations");

            migrationBuilder.DropColumn(
                name: "FirmaId",
                schema: "catalog",
                table: "Declarations");
        }
    }
}
