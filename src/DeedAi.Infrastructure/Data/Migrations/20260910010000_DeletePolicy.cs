using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 5.2.4: persist who may soft-delete documents. New singleton table
    /// with null-safe WhoCanDelete (DEFAULT AllEditors + NULL backfill).
    /// Designer-first. Restore stays Admin-gated.
    /// </summary>
    public partial class DeletePolicy : Migration
    {
        public const string MigrationId = "20260910010000_DeletePolicy";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeletePolicySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WhoCanDelete = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true, defaultValue: "AllEditors"),
                    UpdatedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletePolicySettings", x => x.Id);
                    table.CheckConstraint("CK_DeletePolicySettings_WhoCanDelete", "WhoCanDelete IS NULL OR WhoCanDelete IN ('AllEditors','AdminOnly')");
                });

            migrationBuilder.Sql("""
                INSERT INTO DeletePolicySettings (Id, WhoCanDelete, UpdatedByEmail, UpdatedAt)
                SELECT '40000000-0000-0000-0000-000000000001', 'AllEditors', NULL, '2026-09-10 00:00:00+00:00'
                WHERE NOT EXISTS (SELECT 1 FROM DeletePolicySettings);
                """);

            migrationBuilder.Sql("UPDATE DeletePolicySettings SET WhoCanDelete = 'AllEditors' WHERE WhoCanDelete IS NULL OR WhoCanDelete = '';");

            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                migrationBuilder.Sql("""
                    IF COL_LENGTH(N'dbo.DeletePolicySettings', N'WhoCanDelete') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM sys.default_constraints dc
                           INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                           WHERE dc.parent_object_id = OBJECT_ID(N'dbo.DeletePolicySettings')
                             AND c.name = N'WhoCanDelete')
                    BEGIN
                        ALTER TABLE dbo.DeletePolicySettings ADD CONSTRAINT [DF_DeletePolicySettings_WhoCanDelete] DEFAULT N'AllEditors' FOR [WhoCanDelete];
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                migrationBuilder.Sql("""
                    IF OBJECT_ID(N'dbo.DF_DeletePolicySettings_WhoCanDelete', N'D') IS NOT NULL
                    BEGIN
                        ALTER TABLE dbo.DeletePolicySettings DROP CONSTRAINT [DF_DeletePolicySettings_WhoCanDelete];
                    END
                    """);
            }

            migrationBuilder.DropTable(
                name: "DeletePolicySettings");
        }
    }
}
