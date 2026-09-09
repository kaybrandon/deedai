using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DocumentListFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DocumentNumber",
                table: "Documents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Page",
                table: "Documents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pid",
                table: "Documents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Volume",
                table: "Documents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingStreet",
                table: "Documents",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingCity",
                table: "Documents",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingState",
                table: "Documents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MailingZip",
                table: "Documents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grantors",
                table: "Documents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grantees",
                table: "Documents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalDescription",
                table: "DocumentFields",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentNumber",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Page",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Pid",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Volume",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MailingStreet",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MailingCity",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MailingState",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "MailingZip",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Grantors",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Grantees",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LegalDescription",
                table: "DocumentFields");
        }
    }
}
