using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase491RemovePropertyDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyDefaults");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
