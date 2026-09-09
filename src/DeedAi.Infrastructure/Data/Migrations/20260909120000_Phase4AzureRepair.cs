using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeedAi.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Catch-up for Azure SQL when Phase 4A / Phase 4AQa were invisible to EF
    /// (missing Designer / [Migration] attributes) or only partly applied.
    /// No-ops when the objects already exist. Does not wipe data.
    /// </summary>
    public partial class Phase4AzureRepair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!Phase4SqlServerSchema.IsSqlServer(migrationBuilder))
            {
                return;
            }

            Phase4SqlServerSchema.EnsureAll(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
