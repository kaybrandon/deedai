using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeedAi.Infrastructure.Data;

/// <summary>
/// Design-time factory. Uses Azure SQL host/database names only — no secrets.
/// </summary>
public sealed class DeedAiDbContextFactory : IDesignTimeDbContextFactory<DeedAiDbContext>
{
    public DeedAiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DeedAiDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=tcp:deedaihost01.database.windows.net,1433;Database=dbdeedai;Encrypt=True;TrustServerCertificate=False;");
        return new DeedAiDbContext(optionsBuilder.Options);
    }
}
