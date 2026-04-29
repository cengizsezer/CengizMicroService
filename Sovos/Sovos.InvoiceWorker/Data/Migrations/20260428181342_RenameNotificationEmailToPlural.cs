using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sovos.InvoiceWorker.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameNotificationEmailToPlural : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NotificationEmail",
                table: "SovosCompanies",
                newName: "NotificationEmails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NotificationEmails",
                table: "SovosCompanies",
                newName: "NotificationEmail");
        }
    }
}
