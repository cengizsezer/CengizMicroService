using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CatalogService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "AccountNodes",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false), // Identity YOK, Id bizden geliyor
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountNodes", x => x.Id);

                    table.ForeignKey(
                        name: "FK_AccountNodes_AccountNodes_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "catalog",
                        principalTable: "AccountNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountNodes_Code",
                schema: "catalog",
                table: "AccountNodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountNodes_ParentId_Order",
                schema: "catalog",
                table: "AccountNodes",
                columns: new[] { "ParentId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountNodes_Level_Order",
                schema: "catalog",
                table: "AccountNodes",
                columns: new[] { "Level", "Order" });
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountNodes",
                schema: "catalog");
        }

    }
}
