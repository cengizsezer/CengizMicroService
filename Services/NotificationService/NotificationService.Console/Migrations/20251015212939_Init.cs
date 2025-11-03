using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationService.Console.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "notif",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "notif",
                table: "SentReminders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                schema: "notif",
                table: "Reminders",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_TaskId",
                schema: "notif",
                table: "Reminders",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reminders_Tasks_TaskId",
                schema: "notif",
                table: "Reminders",
                column: "TaskId",
                principalSchema: "notif",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SentReminders_Tasks_TaskId",
                schema: "notif",
                table: "SentReminders",
                column: "TaskId",
                principalSchema: "notif",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reminders_Tasks_TaskId",
                schema: "notif",
                table: "Reminders");

            migrationBuilder.DropForeignKey(
                name: "FK_SentReminders_Tasks_TaskId",
                schema: "notif",
                table: "SentReminders");

            migrationBuilder.DropIndex(
                name: "IX_Reminders_TaskId",
                schema: "notif",
                table: "Reminders");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "notif",
                table: "Tasks",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "notif",
                table: "SentReminders",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "notif",
                table: "Reminders",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
