using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Tampone concorrenza SQLite (audit A1): l'interceptor abilita WAL + busy_timeout a ogni apertura.
/// Verificato su un DB su file (WAL non è applicabile alle connessioni :memory:).
/// </summary>
public sealed class SqliteTuningTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-wal-{Guid.NewGuid():N}.db");

    [Fact]
    public void Interceptor_enables_wal_and_busy_timeout()
    {
        var options = new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .AddInterceptors(new SqliteTuningInterceptor())
            .Options;

        using var db = new VipiDbContext(options);
        db.Database.EnsureCreated();   // apre la connessione ⇒ l'interceptor gira

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();

        Assert.Equal("wal", Scalar(conn, "PRAGMA journal_mode;")?.ToLowerInvariant());
        Assert.Equal(5000L, Convert.ToInt64(Scalar(conn, "PRAGMA busy_timeout;")));
    }

    private static string? Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()?.ToString();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            try { if (File.Exists(_dbPath + suffix)) File.Delete(_dbPath + suffix); } catch { /* best-effort */ }
    }
}
