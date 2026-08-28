using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il controllo che precede l'indice unico sui numeri di rilascio.
///
/// <para>⚠️ La tabella qui la si crea a mano, <b>senza</b> l'indice unico, e non è un espediente da test: è
/// esattamente lo stato del database di produzione prima che la migrazione si applichi. Con
/// <c>EnsureCreated</c> l'indice ci sarebbe già e i doppioni non si potrebbero nemmeno scrivere — cioè si
/// proverebbe il mondo in cui il difetto non esiste.</para>
/// </summary>
public class ReleaseNumberPreflightTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);

        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            "CREATE TABLE DocReleases (Id INTEGER PRIMARY KEY, TargetType TEXT NOT NULL, " +
            "TargetKey TEXT NOT NULL, VersionNumber INTEGER NOT NULL)";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task Rilascio(int id, string tipo, string chiave, int numero)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText =
            $"INSERT INTO DocReleases (Id, TargetType, TargetKey, VersionNumber) VALUES ({id}, '{tipo}', '{chiave}', {numero})";
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Un_archivio_sano_non_ha_niente_da_dire()
    {
        await Rilascio(1, "Airport", "LIRF", 1);
        await Rilascio(2, "Airport", "LIRF", 2);
        await Rilascio(3, "Airport", "LIBD", 1);          // stesso numero, bersaglio diverso: legittimo

        Assert.Empty(ReleaseNumberPreflight.Cerca(_db));
    }

    [Fact]
    public async Task Lo_stesso_numero_sullo_stesso_bersaglio_e_un_doppione()
    {
        await Rilascio(1, "Airport", "LIRF", 3);
        await Rilascio(2, "Airport", "LIRF", 3);          // la corsa fra due pubblicazioni concorrenti
        await Rilascio(3, "AccVipi", "LIBB|LIBB_ES_CTR", 7);
        await Rilascio(4, "AccVipi", "LIBB|LIBB_ES_CTR", 7);
        await Rilascio(5, "AccVipi", "LIBB|LIBB_ES_CTR", 7);

        var trovati = ReleaseNumberPreflight.Cerca(_db);

        Assert.Equal(2, trovati.Count);
        Assert.Contains(trovati, d => d.TargetKey == "LIRF" && d.VersionNumber == 3 && d.Quante == 2);
        Assert.Contains(trovati, d => d.TargetKey == "LIBB|LIBB_ES_CTR" && d.VersionNumber == 7 && d.Quante == 3);
    }

    [Fact]
    public void Senza_la_tabella_non_c_e_niente_da_controllare()
    {
        // Database vuoto: la tabella la crea una migrazione più avanti nella stessa coda. «Non c'è» è il
        // caso in cui i doppioni non esistono, non un guasto da far salire fino a fermare l'avvio.
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var db = new VipiDbContext(
            new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(conn).Options);

        Assert.Empty(ReleaseNumberPreflight.Cerca(db));
    }

    [Fact]
    public void Il_messaggio_dice_le_righe_e_che_cosa_fare()
    {
        // ⚠️ È il testo che qualcuno legge da solo, via FTP, in `avvio-errore.txt`, senza nessuno accanto:
        // se non nomina le righe, il controllo non serve a niente più del «Duplicate entry» di MariaDB.
        var testo = ReleaseNumberPreflight.Messaggio(new[]
        {
            new ReleaseNumberPreflight.Doppione("AccVipi", "LIBB|LIBB_ES_CTR", 7, 3),
        });

        Assert.Contains("AccVipi LIBB|LIBB_ES_CTR", testo);
        Assert.Contains("#7", testo);
        Assert.Contains("×3", testo);
        Assert.Contains(ReleaseNumberPreflight.MigrazioneCheImponeLUnicita, testo);
        Assert.Contains("rinumerano", testo);
    }

    [Fact]
    public async Task Se_la_migrazione_e_gia_applicata_non_si_controlla_nemmeno()
    {
        // L'indice c'è già: i doppioni non possono esistere per costruzione, e il giro d'avvio non deve
        // pagare una scansione a ogni riavvio per sempre.
        await Rilascio(1, "Airport", "LIRF", 3);
        await Rilascio(2, "Airport", "LIRF", 3);

        // Il contesto in memoria non ha nessuna migrazione applicata, quindi la pendente c'è: qui si prova
        // il ramo opposto, cioè che con la migrazione pendente il controllo SCATTI davvero.
        var ex = Assert.Throws<InvalidOperationException>(() => ReleaseNumberPreflight.Verifica(_db));
        Assert.Contains("LIRF", ex.Message);
    }
}
