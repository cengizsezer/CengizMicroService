using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Console.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskUserEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserEmail",
                schema: "notif",
                table: "Tasks",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserEmail",
                schema: "notif",
                table: "Tasks");
        }
    }
}
