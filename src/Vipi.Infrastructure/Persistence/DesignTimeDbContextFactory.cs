using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Factory usata da <c>dotnet ef migrations</c> a design-time (non a runtime).
/// Usa un percorso SQLite di sviluppo; a runtime la connection string arriva dall'host.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VipiDbContext>
{
    public VipiDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite("Data Source=vipi.design.db")
            .Options;
        return new VipiDbContext(options);
    }
}
