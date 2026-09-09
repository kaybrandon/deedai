using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase48AdminEmail : Migration
    {
        public const string Id = "20260909180000_Phase48AdminEmail";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                migrationBuilder.Sql(
                    """
                    IF COL_LENGTH(N'dbo.Users', N'EmailVerified') IS NULL
                        ALTER TABLE [Users] ADD [EmailVerified] bit NOT NULL CONSTRAINT [DF_Users_EmailVerified] DEFAULT CAST(1 AS bit);

                    IF OBJECT_ID(N'dbo.EmailSettings', N'U') IS NULL
                    BEGIN
                        CREATE TABLE [EmailSettings] (
                            [Id] uniqueidentifier NOT NULL,
                            [Mode] nvarchar(16) NOT NULL,
                            [FromName] nvarchar(128) NOT NULL,
                            [FromAddress] nvarchar(256) NOT NULL,
                            [VerifyRequired] bit NOT NULL,
                            [LastSuccessAt] datetimeoffset NULL,
                            [LastFailAt] datetimeoffset NULL,
                            [LastFailReason] nvarchar(200) NULL,
                            [UpdatedAt] datetimeoffset NOT NULL,
                            CONSTRAINT [PK_EmailSettings] PRIMARY KEY ([Id]),
                            CONSTRAINT [CK_EmailSettings_Mode] CHECK (Mode IN (N'SendGrid', N'Smtp'))
                        );
                    END

                    IF OBJECT_ID(N'dbo.EmailVerificationTokens', N'U') IS NULL
                    BEGIN
                        CREATE TABLE [EmailVerificationTokens] (
                            [Id] uniqueidentifier NOT NULL,
                            [UserId] uniqueidentifier NOT NULL,
                            [TokenHash] nvarchar(128) NOT NULL,
                            [ExpiresAt] datetimeoffset NOT NULL,
                            [CreatedAt] datetimeoffset NOT NULL,
                            [UsedAt] datetimeoffset NULL,
                            CONSTRAINT [PK_EmailVerificationTokens] PRIMARY KEY ([Id]),
                            CONSTRAINT [FK_EmailVerificationTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                        );
                        CREATE UNIQUE INDEX [IX_EmailVerificationTokens_TokenHash] ON [EmailVerificationTokens] ([TokenHash]);
                        CREATE INDEX [IX_EmailVerificationTokens_UserId] ON [EmailVerificationTokens] ([UserId]);
                    END
                    """);
                return;
            }

            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "EmailSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    VerifyRequired = table.Column<bool>(type: "bit", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFailAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastFailReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSettings", x => x.Id);
                    table.CheckConstraint("CK_EmailSettings_Mode", "Mode IN ('SendGrid','Smtp')");
                });

            migrationBuilder.CreateTable(
                name: "EmailVerificationTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailVerificationTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailVerificationTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationTokens_TokenHash",
                table: "EmailVerificationTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationTokens_UserId",
                table: "EmailVerificationTokens",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmailSettings");
            migrationBuilder.DropTable(name: "EmailVerificationTokens");
            migrationBuilder.DropColumn(name: "EmailVerified", table: "Users");
        }
    }
}
