using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <summary>
    /// Banka hesabına eşleştirme anahtarları eklenir ve ayrıştırıcı isteğe bağlı olur.
    /// Ayrıştırıcısı olmayan hesaplar bugüne kadar boş metinle saklanıyordu; "yok" tek
    /// bir değerle (NULL) anlatılsın diye eski satırlar da çevrilir.
    /// </summary>
    public partial class AddBankaHesabiEslestirmeAnahtarlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ParserTipi",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "EslestirmeAnahtarlari",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            // Boş metin ile NULL aynı anlama gelmesin: "ayrıştırıcı yok" artık NULL.
            migrationBuilder.Sql(
                "UPDATE [catalog].[EkstreBankaHesaplari] SET [ParserTipi] = NULL WHERE [ParserTipi] = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EslestirmeAnahtarlari",
                schema: "catalog",
                table: "EkstreBankaHesaplari");

            // Sütun tekrar zorunlu olacağı için NULL'lar boş metne çevrilir.
            migrationBuilder.Sql(
                "UPDATE [catalog].[EkstreBankaHesaplari] SET [ParserTipi] = '' WHERE [ParserTipi] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "ParserTipi",
                schema: "catalog",
                table: "EkstreBankaHesaplari",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
