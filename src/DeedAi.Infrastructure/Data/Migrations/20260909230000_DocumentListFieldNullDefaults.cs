using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Production hotfix after PR #35: existing Documents rows have NULL in
    /// columns added by 20260909220000_DocumentListFields. SQL Server
    /// GetString throws SqlNullValueException (HTTP 500.30) when EF maps a
    /// non-nullable string. Backfill NULLs to '' and add store defaults.
    /// Idempotent. Locked field names unchanged.
    /// </summary>
    public partial class DocumentListFieldNullDefaults : Migration
    {
        public const string MigrationId = "20260909230000_DocumentListFieldNullDefaults";

        public static readonly string[] Columns =
        [
            "DocumentNumber",
            "Volume",
            "Page",
            "DeedType",
            "Pid",
            "MailingStreet",
            "MailingCity",
            "MailingState",
            "MailingZip",
            "Grantors",
            "Grantees"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var column in Columns)
            {
                migrationBuilder.Sql($"UPDATE Documents SET [{column}] = '' WHERE [{column}] IS NULL;");
            }

            if (!Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                return;
            }

            foreach (var column in Columns)
            {
                migrationBuilder.Sql($"""
                    IF COL_LENGTH(N'dbo.Documents', N'{column}') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM sys.default_constraints dc
                           INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                           WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Documents')
                             AND c.name = N'{column}')
                    BEGIN
                        ALTER TABLE dbo.Documents ADD CONSTRAINT [DF_Documents_{column}] DEFAULT N'' FOR [{column}];
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (!Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                return;
            }

            foreach (var column in Columns)
            {
                migrationBuilder.Sql($"""
                    IF OBJECT_ID(N'dbo.DF_Documents_{column}', N'D') IS NOT NULL
                    BEGIN
                        ALTER TABLE dbo.Documents DROP CONSTRAINT [DF_Documents_{column}];
                    END
                    """);
            }
        }
    }
}
