using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Phase 5.2.3 Software settings depth: image codes, Grantee combiner,
    /// certified/default year, sales-ratio triad, and null-safe defaults so
    /// existing SoftwareClientConfigs rows do not 500.30 on materialize.
    /// Designer-first. Client / Software labels only.
    /// </summary>
    public partial class Phase523SoftwareDepth : Migration
    {
        public const string MigrationId = "20260910020000_Phase523SoftwareDepth";
        public const string PriorMigrationId = "20260910010000_DeletePolicy";

        public static readonly string[] StringColumns =
        [
            "GranteeCombiner",
            "LookupImageCode",
            "PushImageCode",
            "SalesRatioCode",
            "FinanceCode",
            "InstrumentCode"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GranteeCombiner",
                table: "SoftwareClientConfigs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "first");

            migrationBuilder.AddColumn<int>(
                name: "CertifiedYear",
                table: "SoftwareClientConfigs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DefaultYear",
                table: "SoftwareClientConfigs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LookupImageCode",
                table: "SoftwareClientConfigs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PushImageCode",
                table: "SoftwareClientConfigs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SalesRatioCode",
                table: "SoftwareClientConfigs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FinanceCode",
                table: "SoftwareClientConfigs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InstrumentCode",
                table: "SoftwareClientConfigs",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE SoftwareClientConfigs SET [GranteeCombiner] = 'first' WHERE [GranteeCombiner] IS NULL OR LTRIM(RTRIM([GranteeCombiner])) = '';");
            foreach (var column in StringColumns.Where(x => x != "GranteeCombiner"))
            {
                migrationBuilder.Sql($"UPDATE SoftwareClientConfigs SET [{column}] = '' WHERE [{column}] IS NULL;");
            }

            migrationBuilder.CreateTable(
                name: "SoftwareImageCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: ""),
                    Label = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: ""),
                    DeedType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true, defaultValue: ""),
                    UseOnLookup = table.Column<bool>(type: "bit", nullable: false),
                    UseOnPush = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareImageCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareImageCodes_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareImageCodes_ClientId",
                table: "SoftwareImageCodes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareImageCodes_ClientId_Code",
                table: "SoftwareImageCodes",
                columns: ["ClientId", "Code"],
                unique: true);

            if (!Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                return;
            }

            foreach (var column in StringColumns)
            {
                var defaultSql = column == "GranteeCombiner" ? "N'first'" : "N''";
                migrationBuilder.Sql($"""
                    IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'{column}') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1
                           FROM sys.default_constraints dc
                           INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
                           WHERE dc.parent_object_id = OBJECT_ID(N'dbo.SoftwareClientConfigs')
                             AND c.name = N'{column}')
                    BEGIN
                        ALTER TABLE dbo.SoftwareClientConfigs ADD CONSTRAINT [DF_SoftwareClientConfigs_{column}] DEFAULT {defaultSql} FOR [{column}];
                    END
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SoftwareImageCodes");

            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                foreach (var column in StringColumns)
                {
                    migrationBuilder.Sql($"""
                        IF OBJECT_ID(N'dbo.DF_SoftwareClientConfigs_{column}', N'D') IS NOT NULL
                        BEGIN
                            ALTER TABLE dbo.SoftwareClientConfigs DROP CONSTRAINT [DF_SoftwareClientConfigs_{column}];
                        END
                        """);
                }
            }

            foreach (var column in StringColumns)
            {
                migrationBuilder.DropColumn(name: column, table: "SoftwareClientConfigs");
            }

            migrationBuilder.DropColumn(name: "CertifiedYear", table: "SoftwareClientConfigs");
            migrationBuilder.DropColumn(name: "DefaultYear", table: "SoftwareClientConfigs");
        }
    }
}
