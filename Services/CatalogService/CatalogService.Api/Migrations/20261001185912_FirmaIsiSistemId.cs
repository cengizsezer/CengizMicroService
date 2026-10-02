using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class FirmaIsiSistemId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SistemId",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "int",
                nullable: true);

            // Prompt 8'in kapalı IsProgrami enum'u → ortak sistem listesi. Eski değerler:
            // 0 Belirtilmemiş, 1 Luca, 2 Orka, 3 Logo, 4 Mikro, 5 EBeyanname, 6 SGK,
            // 7 BankaPortali, 8 Diğer (ProgramDiger serbest metni).
            // Aynı adlı (aynı türde) kayda bağlanır; yoksa kayıt AÇILIR ve
            // OlusturanKullaniciId = 'tasima:prompt9' ile işaretlenir — SistemSeed her açılışta
            // bunları uyarı olarak loglar, Yönetim → Sistemler'de görünürler.
            // Tur: 1 Muhasebe, 4 Banka, 5 Beyanname, 6 Diğer.
            migrationBuilder.Sql(@"
SELECT i.Id,
       CAST(CASE i.Program
                WHEN 1 THEN N'Luca'
                WHEN 2 THEN N'ORKA'
                WHEN 3 THEN N'Logo'
                WHEN 4 THEN N'Mikro'
                WHEN 5 THEN N'e-Beyanname GİB'
                WHEN 6 THEN N'SGK'
                WHEN 7 THEN N'Banka portalı'
                ELSE LEFT(COALESCE(NULLIF(LTRIM(RTRIM(i.ProgramDiger)), N''), N'Diğer'), 100)
            END AS nvarchar(100)) AS Ad,
       CAST(CASE WHEN i.Program BETWEEN 1 AND 4 THEN 1
                 WHEN i.Program = 5 THEN 5
                 WHEN i.Program = 7 THEN 4
                 ELSE 6 END AS tinyint) AS Tur
INTO #ProgramTasima
FROM [catalog].[FirmaIsleri] i
WHERE i.Program <> 0;

INSERT INTO [catalog].[Sistemler] ([Ad], [Tur], [Aktif], [OlusturmaZamani], [OlusturanKullaniciId])
SELECT DISTINCT t.Ad, t.Tur, 1, SYSUTCDATETIME(), N'tasima:prompt9'
FROM #ProgramTasima t
WHERE NOT EXISTS (SELECT 1 FROM [catalog].[Sistemler] s WHERE s.[Tur] = t.Tur AND s.[Ad] = t.Ad);

UPDATE i SET i.[SistemId] = s.[Id]
FROM [catalog].[FirmaIsleri] i
JOIN #ProgramTasima t ON t.Id = i.Id
JOIN [catalog].[Sistemler] s ON s.[Tur] = t.Tur AND s.[Ad] = t.Ad;

DROP TABLE #ProgramTasima;
");

            migrationBuilder.DropColumn(
                name: "Program",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "ProgramDiger",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsleri_SistemId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "SistemId");

            migrationBuilder.AddForeignKey(
                name: "FK_FirmaIsleri_Sistemler_SistemId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "SistemId",
                principalSchema: "catalog",
                principalTable: "Sistemler",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirmaIsleri_Sistemler_SistemId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropIndex(
                name: "IX_FirmaIsleri_SistemId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "SistemId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.AddColumn<byte>(
                name: "Program",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "ProgramDiger",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
