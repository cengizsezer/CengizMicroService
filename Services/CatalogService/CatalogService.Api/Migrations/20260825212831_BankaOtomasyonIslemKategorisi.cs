using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class BankaOtomasyonIslemKategorisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IslemKategorisiId",
                schema: "catalog",
                table: "EkstreVergiKodlari",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IslemKategorisiId",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IslemKategorisiId",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IslemKategorisiId",
                schema: "catalog",
                table: "EkstreAciklamaSablonlari",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EkstreIslemKategorileri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VarsayilanAnaGrup = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkstreIslemKategorileri", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EkstreVergiKodlari_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreVergiKodlari",
                column: "IslemKategorisiId");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreSabitKurallar_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                column: "IslemKategorisiId");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreKisiYonlendirmeleri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri",
                column: "IslemKategorisiId");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreAciklamaSablonlari_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreAciklamaSablonlari",
                column: "IslemKategorisiId");

            migrationBuilder.CreateIndex(
                name: "IX_EkstreIslemKategorileri_Ad",
                schema: "catalog",
                table: "EkstreIslemKategorileri",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkstreIslemKategorileri_Sira",
                schema: "catalog",
                table: "EkstreIslemKategorileri",
                column: "Sira");

            migrationBuilder.AddForeignKey(
                name: "FK_EkstreAciklamaSablonlari_EkstreIslemKategorileri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreAciklamaSablonlari",
                column: "IslemKategorisiId",
                principalSchema: "catalog",
                principalTable: "EkstreIslemKategorileri",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EkstreKisiYonlendirmeleri_EkstreIslemKategorileri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri",
                column: "IslemKategorisiId",
                principalSchema: "catalog",
                principalTable: "EkstreIslemKategorileri",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EkstreSabitKurallar_EkstreIslemKategorileri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreSabitKurallar",
                column: "IslemKategorisiId",
                principalSchema: "catalog",
                principalTable: "EkstreIslemKategorileri",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EkstreVergiKodlari_EkstreIslemKategorileri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreVergiKodlari",
                column: "IslemKategorisiId",
                principalSchema: "catalog",
                principalTable: "EkstreIslemKategorileri",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EkstreAciklamaSablonlari_EkstreIslemKategorileri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreAciklamaSablonlari");

            migrationBuilder.DropForeignKey(
                name: "FK_EkstreKisiYonlendirmeleri_EkstreIslemKategorileri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri");

            migrationBuilder.DropForeignKey(
                name: "FK_EkstreSabitKurallar_EkstreIslemKategorileri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreSabitKurallar");

            migrationBuilder.DropForeignKey(
                name: "FK_EkstreVergiKodlari_EkstreIslemKategorileri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreVergiKodlari");

            migrationBuilder.DropTable(
                name: "EkstreIslemKategorileri",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_EkstreVergiKodlari_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreVergiKodlari");

            migrationBuilder.DropIndex(
                name: "IX_EkstreSabitKurallar_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreSabitKurallar");

            migrationBuilder.DropIndex(
                name: "IX_EkstreKisiYonlendirmeleri_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri");

            migrationBuilder.DropIndex(
                name: "IX_EkstreAciklamaSablonlari_IslemKategorisiId",
                schema: "catalog",
                table: "EkstreAciklamaSablonlari");

            migrationBuilder.DropColumn(
                name: "IslemKategorisiId",
                schema: "catalog",
                table: "EkstreVergiKodlari");

            migrationBuilder.DropColumn(
                name: "IslemKategorisiId",
                schema: "catalog",
                table: "EkstreSabitKurallar");

            migrationBuilder.DropColumn(
                name: "IslemKategorisiId",
                schema: "catalog",
                table: "EkstreKisiYonlendirmeleri");

            migrationBuilder.DropColumn(
                name: "IslemKategorisiId",
                schema: "catalog",
                table: "EkstreAciklamaSablonlari");
        }
    }
}
