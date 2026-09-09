using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Hardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                Phase4SqlServerSchema.EnsureHardening(migrationBuilder);
                return;
            }

            UpStandard(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OcrCleanupRules");

            migrationBuilder.DropTable(
                name: "SessionSettings");
        }

        private static void UpStandard(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OcrCleanupRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OcrCleanupRules", x => x.Id);
                    table.CheckConstraint("CK_OcrCleanupRules_Kind", "Kind IN ('Trim','Discard')");
                });

            migrationBuilder.CreateTable(
                name: "SessionSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdleTimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OcrCleanupRules_Kind_Value",
                table: "OcrCleanupRules",
                columns: new[] { "Kind", "Value" },
                unique: true);
        }
    }
}
