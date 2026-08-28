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
        // ⚠️ `Pooling=False` NON è un dettaglio di pulizia: è ciò che rende la prova onesta. Vedi sotto.
        var options = new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite($"Data Source={_dbPath};Pooling=False")
            .AddInterceptors(new SqliteTuningInterceptor())
            .Options;

        using var db = new VipiDbContext(options);
        db.Database.EnsureCreated();   // apre la connessione ⇒ l'interceptor gira

        // ⚠️ Si riapre ATTRAVERSO EF, non con `conn.Open()` sulla connessione nuda: l'interceptor è di EF
        // Core e gira solo quando è EF ad aprire. `Open()` diretto lo scavalca — e prima del 28 agosto 2026
        // il test faceva esattamente questo, passando lo stesso: Microsoft.Data.Sqlite tiene le connessioni
        // in un POOL, e dopo EnsureCreated ne restituiva la STESSA handle, che il busy_timeout ce l'aveva
        // già addosso. Verde per via del pool, non per via dell'interceptor.
        //
        // È il rosso «una volta sola» di lavori-aperti Q6: basta che qualcuno svuoti il pool nella finestra
        // fra la chiusura di EF e la riapertura — `SqliteConnection.ClearAllPools()` è di PROCESSO, e un
        // altro test dell'assembly lo chiamava — perché arrivi una handle nuova, col busy_timeout a zero.
        // Riprodotto in modo deterministico aggiungendo `Pooling=False`: «Expected: 5000, Actual: 0».
        // Il WAL invece regge comunque, perché è scritto nell'intestazione del FILE e non nella connessione:
        // l'asserzione che cadeva era la seconda, non la prima. Le due diagnosi che il documento chiedeva
        // di distinguere sono distinte, ed è questa.
        //
        // `Pooling=False` resta acceso perché il test non possa più passare per sbaglio: senza pool, se
        // l'interceptor non gira l'asserzione cade sempre invece che una volta ogni tanto.
        db.Database.OpenConnection();
        var conn = (SqliteConnection)db.Database.GetDbConnection();

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
        // ⚠️ Qui c'era `SqliteConnection.ClearAllPools()`, che serviva a liberare il file per cancellarlo.
        // È una chiamata di PROCESSO: svuota anche i pool dei test che stanno girando in parallelo, ed è
        // il meccanismo con cui questo stesso test faceva cadere sé stesso (Q6). Con `Pooling=False` nella
        // stringa di connessione il file si libera alla chiusura del DbContext e non serve toccare nessuno.
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            try { if (File.Exists(_dbPath + suffix)) File.Delete(_dbPath + suffix); } catch { /* best-effort */ }
    }
}
