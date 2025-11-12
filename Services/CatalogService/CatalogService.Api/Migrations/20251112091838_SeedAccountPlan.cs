using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedAccountPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "catalog",
                table: "AccountNodes",
                columns: new[] { "Id", "Code", "Description", "Level", "Name", "Notes", "Order", "ParentId" },
                values: new object[,]
                {
                    { 1, "1", null, 1, "Dönen Varlıklar", null, 1, null },
                    { 2, "10", null, 2, "Hazır Değerler", null, 10, 1 },
                    { 8, "11", null, 2, "Menkul Kıymetler", null, 11, 1 },
                    { 14, "12", null, 2, "Ticari Alacaklar", null, 12, 1 },
                    { 3, "100", "İşletmenin kasa mevcudu.", 3, "Kasa", null, 100, 2 },
                    { 4, "101", null, 3, "Alınan Çekler", null, 101, 2 },
                    { 5, "102", null, 3, "Bankalar", null, 102, 2 },
                    { 6, "103", null, 3, "Verilen Çekler ve Ödeme Emirleri (-)", null, 103, 2 },
                    { 7, "108", null, 3, "Diğer Hazır Değerler", null, 108, 2 },
                    { 9, "110", null, 3, "Hisse Senetleri", null, 110, 8 },
                    { 10, "111", null, 3, "Özel Kesim Tahvil, Senet ve Bonoları", null, 111, 8 },
                    { 11, "112", null, 3, "Kamu Kesimi Tahvil, Senet ve Bonoları", null, 112, 8 },
                    { 12, "118", null, 3, "Diğer Menkul Kıymetler", null, 118, 8 },
                    { 13, "119", null, 3, "Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", null, 119, 8 },
                    { 15, "120", null, 3, "Alıcılar", null, 120, 14 },
                    { 16, "121", null, 3, "Alacak Senetleri", null, 121, 14 },
                    { 17, "127", null, 3, "Diğer Ticari Alacaklar", null, 127, 14 },
                    { 18, "128", null, 3, "Şüpheli Ticari Alacaklar", null, 128, 14 },
                    { 19, "129", null, 3, "Şüpheli Ticari Alacaklar Karşılığı (-)", null, 129, 14 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "AccountNodes",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
