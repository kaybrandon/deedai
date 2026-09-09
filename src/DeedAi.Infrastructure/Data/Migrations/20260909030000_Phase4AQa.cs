using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DeedAiDbContext))]
    [Migration(Phase4SqlServerSchema.Phase4AQaId)]
    public partial class Phase4AQa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                Phase4SqlServerSchema.EnsurePhase4AQa(migrationBuilder);
                return;
            }

            UpStandard(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SalesTabCodes");
            migrationBuilder.DropTable(name: "SoftwareClientConfigs");
            migrationBuilder.DropColumn(name: "SalesTabCode", table: "Documents");
        }

        private static void UpStandard(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SalesTabCode",
                table: "Documents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SoftwareClientConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Vendor = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ApiUrl = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    GroupCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RemoveLeadingZeros = table.Column<bool>(type: "bit", nullable: false),
                    DateLabelDepth = table.Column<int>(type: "int", nullable: false),
                    DisplaySalesTab = table.Column<bool>(type: "bit", nullable: false),
                    SendConsideration = table.Column<bool>(type: "bit", nullable: false),
                    ConsiderationThreshold = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ResetExemptions = table.Column<bool>(type: "bit", nullable: false),
                    ResetSupplementYear = table.Column<bool>(type: "bit", nullable: false),
                    ResetSalesLetter = table.Column<bool>(type: "bit", nullable: false),
                    ResetSalesTab = table.Column<bool>(type: "bit", nullable: false),
                    ResetAgents = table.Column<bool>(type: "bit", nullable: false),
                    ResetMortgageCodes = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoftwareClientConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoftwareClientConfigs_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesTabCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MinConsideration = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxConsideration = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesTabCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesTabCodes_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoftwareClientConfigs_ClientId",
                table: "SoftwareClientConfigs",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesTabCodes_ClientId",
                table: "SalesTabCodes",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesTabCodes_ClientId_Code",
                table: "SalesTabCodes",
                columns: new[] { "ClientId", "Code" },
                unique: true);
        }
    }
}
