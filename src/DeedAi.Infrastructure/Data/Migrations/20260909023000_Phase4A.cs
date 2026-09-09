using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DeedAiDbContext))]
    [Migration(Phase4SqlServerSchema.Phase4AId)]
    public partial class Phase4A : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                Phase4SqlServerSchema.EnsurePhase4A(migrationBuilder);
                return;
            }

            UpStandard(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PropertyDefaults");
            migrationBuilder.DropTable(name: "SoftwareFieldMaps");
            migrationBuilder.DropTable(name: "AppPolicies");
        }

        private static void UpStandard(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SoftwarePushEnabled = table.Column<bool>(type: "bit", nullable: false),
                    SoftwareDefaultGroup = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SoftwareFieldDefaultsJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SoftwareFieldMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeedField = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SoftwareField = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SoftwareGroup = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeedType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareFieldMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareFieldMaps_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyDefaults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeedType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyDefaults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyDefaults_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareFieldMaps_DeedField_ClientId_DeedType",
                table: "SoftwareFieldMaps",
                columns: new[] { "DeedField", "ClientId", "DeedType" });

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareFieldMaps_ClientId",
                table: "SoftwareFieldMaps",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefaults_Scope_ClientId_DeedType_FieldKey",
                table: "PropertyDefaults",
                columns: new[] { "Scope", "ClientId", "DeedType", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDefaults_ClientId",
                table: "PropertyDefaults",
                column: "ClientId");
        }
    }
}
