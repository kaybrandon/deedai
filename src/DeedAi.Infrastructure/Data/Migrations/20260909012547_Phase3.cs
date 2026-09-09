using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3 : Migration
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
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UploadedByUserId",
                table: "Documents");

            migrationBuilder.DropTable(
                name: "NotificationSettings");

            migrationBuilder.DropTable(
                name: "TeamUsers");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UploadedByUserId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LastSoftwareSyncAt",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LastSoftwareSyncDirection",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LastSoftwareSyncFailReason",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LastSoftwareSyncStatus",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SoftwareRecordId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Clients");
        }

        private static bool IsSqlServer(MigrationBuilder migrationBuilder) =>
            string.Equals(
                migrationBuilder.ActiveProvider,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);

        private static void UpStandard(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSoftwareSyncAt",
                table: "Documents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSoftwareSyncDirection",
                table: "Documents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSoftwareSyncFailReason",
                table: "Documents",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSoftwareSyncStatus",
                table: "Documents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoftwareRecordId",
                table: "Documents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Clients",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "NotificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    NotifyUploader = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamUsers",
                columns: table => new
                {
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamUsers", x => new { x.TeamId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TeamUsers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamUsers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedByUserId",
                table: "Documents",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamUsers_UserId",
                table: "TeamUsers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UploadedByUserId",
                table: "Documents",
                column: "UploadedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <summary>
        /// Azure Phase 3 failed on the UploadedBy FK after earlier DDL. Re-run must
        /// skip objects that already exist and create the FK as ON DELETE NO ACTION.
        /// </summary>
        private static void UpSqlServerIdempotent(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.Documents', N'LastSoftwareSyncAt') IS NULL
                    ALTER TABLE [Documents] ADD [LastSoftwareSyncAt] datetimeoffset NULL;

                IF COL_LENGTH(N'dbo.Documents', N'LastSoftwareSyncDirection') IS NULL
                    ALTER TABLE [Documents] ADD [LastSoftwareSyncDirection] nvarchar(16) NULL;

                IF COL_LENGTH(N'dbo.Documents', N'LastSoftwareSyncFailReason') IS NULL
                    ALTER TABLE [Documents] ADD [LastSoftwareSyncFailReason] nvarchar(1024) NULL;

                IF COL_LENGTH(N'dbo.Documents', N'LastSoftwareSyncStatus') IS NULL
                    ALTER TABLE [Documents] ADD [LastSoftwareSyncStatus] nvarchar(16) NULL;

                IF COL_LENGTH(N'dbo.Documents', N'SoftwareRecordId') IS NULL
                    ALTER TABLE [Documents] ADD [SoftwareRecordId] nvarchar(64) NULL;

                IF COL_LENGTH(N'dbo.Clients', N'IsActive') IS NULL
                    ALTER TABLE [Clients] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_Clients_IsActive] DEFAULT CAST(1 AS bit);

                IF OBJECT_ID(N'dbo.NotificationSettings', N'U') IS NULL
                BEGIN
                    CREATE TABLE [NotificationSettings] (
                        [Id] uniqueidentifier NOT NULL,
                        [Enabled] bit NOT NULL,
                        [NotifyUploader] bit NOT NULL,
                        [UpdatedAt] datetimeoffset NOT NULL,
                        CONSTRAINT [PK_NotificationSettings] PRIMARY KEY ([Id])
                    );
                END

                IF OBJECT_ID(N'dbo.Teams', N'U') IS NULL
                BEGIN
                    CREATE TABLE [Teams] (
                        [Id] uniqueidentifier NOT NULL,
                        [Name] nvarchar(128) NOT NULL,
                        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
                        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
                    );
                END

                IF OBJECT_ID(N'dbo.TeamUsers', N'U') IS NULL
                BEGIN
                    CREATE TABLE [TeamUsers] (
                        [TeamId] uniqueidentifier NOT NULL,
                        [UserId] uniqueidentifier NOT NULL,
                        CONSTRAINT [PK_TeamUsers] PRIMARY KEY ([TeamId], [UserId]),
                        CONSTRAINT [FK_TeamUsers_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_TeamUsers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );
                END

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Documents_UploadedByUserId'
                      AND object_id = OBJECT_ID(N'dbo.Documents'))
                    CREATE INDEX [IX_Documents_UploadedByUserId] ON [Documents] ([UploadedByUserId]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Teams_Name'
                      AND object_id = OBJECT_ID(N'dbo.Teams'))
                    CREATE UNIQUE INDEX [IX_Teams_Name] ON [Teams] ([Name]);

                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_TeamUsers_UserId'
                      AND object_id = OBJECT_ID(N'dbo.TeamUsers'))
                    CREATE INDEX [IX_TeamUsers_UserId] ON [TeamUsers] ([UserId]);

                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE name = N'FK_Documents_Users_UploadedByUserId'
                      AND parent_object_id = OBJECT_ID(N'dbo.Documents'))
                    ALTER TABLE [Documents] DROP CONSTRAINT [FK_Documents_Users_UploadedByUserId];

                ALTER TABLE [Documents] WITH CHECK ADD CONSTRAINT [FK_Documents_Users_UploadedByUserId]
                    FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
                """);
        }
    }
}
