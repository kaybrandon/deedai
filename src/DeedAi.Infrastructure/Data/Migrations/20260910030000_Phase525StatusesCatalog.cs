using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 5.2.5 Statuses catalog: MapsTo / Kind / IsSeed on StatusDefinitions
    /// with null-safe defaults (backfill + SQL DEFAULT) so existing rows do not
    /// 500.30 on materialize. Seeds the Must eight Client/Software labels.
    /// Designer-first. Never County / CAMA.
    /// </summary>
    public partial class Phase525StatusesCatalog : Migration
    {
        public const string MigrationId = "20260910030000_Phase525StatusesCatalog";
        public const string PriorMigrationId = "20260910020000_Phase523SoftwareDepth";

        public static readonly string[] StringColumns = ["MapsTo", "Kind"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MapsTo",
                table: "StatusDefinitions",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "StatusDefinitions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSeed",
                table: "StatusDefinitions",
                type: "bit",
                nullable: true,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE StatusDefinitions SET [MapsTo] = [Code] WHERE [MapsTo] IS NULL OR [MapsTo] = '';");
            migrationBuilder.Sql("UPDATE StatusDefinitions SET [Kind] = '' WHERE [Kind] IS NULL;");
            migrationBuilder.Sql("UPDATE StatusDefinitions SET [IsSeed] = 0 WHERE [IsSeed] IS NULL;");
            migrationBuilder.Sql("UPDATE StatusDefinitions SET [Kind] = 'Pipeline' WHERE [IsSystem] = 1 AND ([Kind] IS NULL OR [Kind] = '');");
            migrationBuilder.Sql("UPDATE StatusDefinitions SET [Kind] = 'Review' WHERE [Code] IN ('NeedsReview', 'Approved') AND ([Kind] IS NULL OR [Kind] = '');");
            migrationBuilder.Sql("UPDATE StatusDefinitions SET [Kind] = 'Catalog' WHERE [Kind] IS NULL OR [Kind] = '';");

            SeedMust(migrationBuilder, "Complete", "Complete", "#D8F0EA", "Review", "Ready", 60, "10000000-0000-0000-0000-000000000011");
            SeedMust(migrationBuilder, "InQueue", "In Queue", "#C5CED6", "Pipeline", "Queued", 20, "10000000-0000-0000-0000-000000000012");
            SeedMust(migrationBuilder, "NeedsWork", "Needs Work", "#C5E8E4", "Review", "NeedsReview", 40, "10000000-0000-0000-0000-000000000013");
            SeedMust(migrationBuilder, "New", "New", "#C5D4F0", "Review", "New", 10, "10000000-0000-0000-0000-000000000014");
            SeedMust(migrationBuilder, "NotNeeded", "Not Needed", "#D5DBE3", "Review", "NotNeeded", 70, "10000000-0000-0000-0000-000000000015");
            SeedMust(migrationBuilder, "Pending", "Pending", "#E8C96A", "Pipeline", "Processing", 30, "10000000-0000-0000-0000-000000000016");
            SeedMust(migrationBuilder, "Research", "Research", "#B7D9D4", "Review", "Research", 50, "10000000-0000-0000-0000-000000000017");
            SeedMust(migrationBuilder, "UploadError", "Upload Error", "#F5D6D3", "Pipeline", "Failed", 80, "10000000-0000-0000-0000-000000000018");

            if (!Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                return;
            }

            foreach (var column in StringColumns)
            {
                migrationBuilder.Sql($"""
                    IF COL_LENGTH(N'dbo.StatusDefinitions', N'{column}') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM sys.default_constraints dc
                           INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                           WHERE dc.parent_object_id = OBJECT_ID(N'dbo.StatusDefinitions')
                             AND c.name = N'{column}')
                    BEGIN
                        ALTER TABLE dbo.StatusDefinitions ADD CONSTRAINT [DF_StatusDefinitions_{column}] DEFAULT N'' FOR [{column}];
                    END
                    """);
            }

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.StatusDefinitions', N'IsSeed') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM sys.default_constraints dc
                       INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                       WHERE dc.parent_object_id = OBJECT_ID(N'dbo.StatusDefinitions')
                         AND c.name = N'IsSeed')
                BEGIN
                    ALTER TABLE dbo.StatusDefinitions ADD CONSTRAINT [DF_StatusDefinitions_IsSeed] DEFAULT 0 FOR [IsSeed];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                foreach (var column in StringColumns.Concat(["IsSeed"]))
                {
                    migrationBuilder.Sql($"""
                        IF OBJECT_ID(N'dbo.DF_StatusDefinitions_{column}', N'D') IS NOT NULL
                        BEGIN
                            ALTER TABLE dbo.StatusDefinitions DROP CONSTRAINT [DF_StatusDefinitions_{column}];
                        END
                        """);
                }
            }

            migrationBuilder.DropColumn(name: "IsSeed", table: "StatusDefinitions");
            migrationBuilder.DropColumn(name: "Kind", table: "StatusDefinitions");
            migrationBuilder.DropColumn(name: "MapsTo", table: "StatusDefinitions");
        }

        private static void SeedMust(
            MigrationBuilder migrationBuilder,
            string code,
            string displayName,
            string color,
            string kind,
            string mapsTo,
            int sortOrder,
            string id)
        {
            migrationBuilder.Sql($"""
                INSERT INTO StatusDefinitions (Id, Code, DisplayName, Color, IsSystem, SortOrder, IsActive, MapsTo, Kind, IsSeed)
                SELECT '{id}', '{code}', '{displayName}', '{color}', 0, {sortOrder}, 1, '{mapsTo}', '{kind}', 1
                WHERE NOT EXISTS (
                    SELECT 1 FROM StatusDefinitions
                    WHERE Code = '{code}' OR DisplayName = '{displayName}');
                """);
        }
    }
}
