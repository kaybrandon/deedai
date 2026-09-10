using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 6 AI extract: raw AI blob pointer and per-field confidence JSON
    /// with null-safe defaults (backfill + SQL DEFAULT). Designer-first.
    /// Never County / CAMA. Do not raise Azure OpenAI quotas.
    /// </summary>
    public partial class Phase6AiExtract : Migration
    {
        public const string MigrationId = "20260910040000_Phase6AiExtract";
        public const string PriorMigrationId = "20260910030000_Phase525StatusesCatalog";

        public static readonly string[] StringColumns = ["AiRawBlobPath", "ExtractConfidenceJson"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AiRawBlobPath",
                table: "Documents",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExtractConfidenceJson",
                table: "Documents",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE Documents SET [AiRawBlobPath] = '' WHERE [AiRawBlobPath] IS NULL;");
            migrationBuilder.Sql("UPDATE Documents SET [ExtractConfidenceJson] = '' WHERE [ExtractConfidenceJson] IS NULL;");

            if (!Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                return;
            }

            foreach (var column in StringColumns)
            {
                var defaultSql = column == "ExtractConfidenceJson" ? "N''" : "N''";
                migrationBuilder.Sql($"""
                    IF COL_LENGTH(N'dbo.Documents', N'{column}') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM sys.default_constraints dc
                           INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                           WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Documents')
                             AND c.name = N'{column}')
                    BEGIN
                        ALTER TABLE dbo.Documents ADD CONSTRAINT [DF_Documents_{column}] DEFAULT {defaultSql} FOR [{column}];
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                foreach (var column in StringColumns)
                {
                    migrationBuilder.Sql($"""
                        IF OBJECT_ID(N'dbo.DF_Documents_{column}', N'D') IS NOT NULL
                        BEGIN
                            ALTER TABLE dbo.Documents DROP CONSTRAINT [DF_Documents_{column}];
                        END
                        """);
                }
            }

            migrationBuilder.DropColumn(name: "ExtractConfidenceJson", table: "Documents");
            migrationBuilder.DropColumn(name: "AiRawBlobPath", table: "Documents");
        }
    }
}
