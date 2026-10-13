using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIsKategorileri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KategoriId",
                schema: "catalog",
                table: "IsTarifleri",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KategoriId",
                schema: "catalog",
                table: "FirmaIsleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IsKategorileri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    VarsayilanYasal = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IsKategorileri", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MukellefiyetKategorileri",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MukellefiyetKodu = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    KategoriId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MukellefiyetKategorileri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MukellefiyetKategorileri_IsKategorileri_KategoriId",
                        column: x => x.KategoriId,
                        principalSchema: "catalog",
                        principalTable: "IsKategorileri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IsTarifleri_KategoriId",
                schema: "catalog",
                table: "IsTarifleri",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_FirmaIsleri_KategoriId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_IsKategorileri_Ad",
                schema: "catalog",
                table: "IsKategorileri",
                column: "Ad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IsKategorileri_VarsayilanYasal",
                schema: "catalog",
                table: "IsKategorileri",
                column: "VarsayilanYasal",
                unique: true,
                filter: "[VarsayilanYasal] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MukellefiyetKategorileri_KategoriId",
                schema: "catalog",
                table: "MukellefiyetKategorileri",
                column: "KategoriId");

            migrationBuilder.CreateIndex(
                name: "IX_MukellefiyetKategorileri_MukellefiyetKodu",
                schema: "catalog",
                table: "MukellefiyetKategorileri",
                column: "MukellefiyetKodu",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FirmaIsleri_IsKategorileri_KategoriId",
                schema: "catalog",
                table: "FirmaIsleri",
                column: "KategoriId",
                principalSchema: "catalog",
                principalTable: "IsKategorileri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IsTarifleri_IsKategorileri_KategoriId",
                schema: "catalog",
                table: "IsTarifleri",
                column: "KategoriId",
                principalSchema: "catalog",
                principalTable: "IsKategorileri",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FirmaIsleri_IsKategorileri_KategoriId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropForeignKey(
                name: "FK_IsTarifleri_IsKategorileri_KategoriId",
                schema: "catalog",
                table: "IsTarifleri");

            migrationBuilder.DropTable(
                name: "MukellefiyetKategorileri",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "IsKategorileri",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_IsTarifleri_KategoriId",
                schema: "catalog",
                table: "IsTarifleri");

            migrationBuilder.DropIndex(
                name: "IX_FirmaIsleri_KategoriId",
                schema: "catalog",
                table: "FirmaIsleri");

            migrationBuilder.DropColumn(
                name: "KategoriId",
                schema: "catalog",
                table: "IsTarifleri");

            migrationBuilder.DropColumn(
                name: "KategoriId",
                schema: "catalog",
                table: "FirmaIsleri");
        }
    }
}
