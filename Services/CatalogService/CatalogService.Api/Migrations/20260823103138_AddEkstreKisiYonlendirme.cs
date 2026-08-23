using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEkstreKisiYonlendirme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EkstreKisiYonlendirmeleri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsimCekirdegi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Isim = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Yon = table.Column<byte>(type: "tinyint", nullable: false),
                    HesapKodu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HesapAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    TenantNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreKisiYonlendirmeleri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreKisiYonlendirmeleri_TenantNo_IsimCekirdegi_Yon",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri",
                columns: new[] { "TenantNo", "IsimCekirdegi", "Yon" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkstreKisiYonlendirmeleri",
                schema: "catalog");
        }
    }
}
