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
            if (IsSqlServer(migrationBuilder))
            {
                UpSqlServerIdempotent(migrationBuilder);
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

        private static bool IsSqlServer(MigrationBuilder migrationBuilder) =>
            string.Equals(
                migrationBuilder.ActiveProvider,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);

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

        /// <summary>
        /// Survive a re-run after a partial apply (same pattern as Phase 3 / PR #8).
        /// </summary>
        private static void UpSqlServerIdempotent(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'dbo.OcrCleanupRules', N'U') IS NULL
                BEGIN
                    CREATE TABLE [OcrCleanupRules] (
                        [Id] uniqueidentifier NOT NULL,
                        [Kind] nvarchar(16) NOT NULL,
                        [Value] nvarchar(64) NOT NULL,
                        [IsActive] bit NOT NULL,
                        [SortOrder] int NOT NULL,
                        CONSTRAINT [PK_OcrCleanupRules] PRIMARY KEY ([Id]),
                        CONSTRAINT [CK_OcrCleanupRules_Kind] CHECK (Kind IN (N'Trim', N'Discard'))
                    );
                END

                IF OBJECT_ID(N'dbo.SessionSettings', N'U') IS NULL
                BEGIN
                    CREATE TABLE [SessionSettings] (
                        [Id] uniqueidentifier NOT NULL,
                        [IdleTimeoutMinutes] int NOT NULL,
                        [UpdatedAt] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_SessionSettings] PRIMARY KEY ([Id])
                    );
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_OcrCleanupRules_Kind_Value'
                      AND object_id = OBJECT_ID(N'dbo.OcrCleanupRules'))
                    CREATE UNIQUE INDEX [IX_OcrCleanupRules_Kind_Value] ON [OcrCleanupRules] ([Kind], [Value]);
                """);
        }
    }
}
