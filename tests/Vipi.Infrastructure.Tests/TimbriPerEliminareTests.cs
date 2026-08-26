using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// I due timbri che rendono calcolabile la regola «si elimina solo ciò che la sorgente non manda da due
/// giri»: il <b>penultimo</b> giro riuscito di una categoria (<c>ImportState.PrevSuccessUtc</c>) e l'ultima
/// volta che la sorgente ha nominato un <b>aeroporto</b> (<c>Airport.LastSeenAtUtc</c>).
///
/// <para>Prima del 26 agosto 2026 non esistevano né l'uno né l'altro: lo stato teneva solo l'ultimo giro, e
/// per gli aeroporti non c'era proprio nessun timbro — l'assegnazione è additiva e salta gli ICAO già in
/// archivio, quindi nulla distingueva uno scalo confermato stanotte da uno sparito a luglio.</para>
/// </summary>
public class TimbriPerEliminareTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    [Fact]
    public async Task Il_primo_giro_non_lascia_nessun_penultimo()
    {
        var store = new EfImportStateStore(_db);
        await store.MarkSuccessAsync(ImportCategories.Acc, new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc));

        Assert.Null(await store.GetPrevSuccessAsync(ImportCategories.Acc));
    }

    [Fact]
    public async Task Il_secondo_giro_fa_scorrere_il_penultimo()
    {
        var store = new EfImportStateStore(_db);
        var primo = new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc);
        var secondo = primo.AddDays(1);

        await store.MarkSuccessAsync(ImportCategories.Acc, primo);
        await store.MarkSuccessAsync(ImportCategories.Acc, secondo);

        Assert.Equal(primo, await store.GetPrevSuccessAsync(ImportCategories.Acc));
        Assert.Equal(secondo, await store.GetLastSuccessAsync(ImportCategories.Acc));
    }

    [Fact]
    public async Task Due_giri_troppo_vicini_non_consumano_le_due_conferme()
    {
        // ⚠️ La trappola dei due clic: premere due volte il bottone di re-import a cinque minuti di distanza
        // non deve rendere eliminabile mezzo catalogo.
        var store = new EfImportStateStore(_db);
        var primo = new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc);

        await store.MarkSuccessAsync(ImportCategories.Acc, primo);
        await store.MarkSuccessAsync(ImportCategories.Acc, primo.AddMinutes(5));

        Assert.Null(await store.GetPrevSuccessAsync(ImportCategories.Acc));
        Assert.Equal(primo.AddMinutes(5), await store.GetLastSuccessAsync(ImportCategories.Acc));
    }

    [Fact]
    public async Task Un_fallimento_non_tocca_i_due_timbri()
    {
        var store = new EfImportStateStore(_db);
        var primo = new DateTime(2026, 8, 20, 3, 0, 0, DateTimeKind.Utc);
        await store.MarkSuccessAsync(ImportCategories.Acc, primo);
        await store.MarkSuccessAsync(ImportCategories.Acc, primo.AddDays(1));

        await store.MarkFailureAsync(ImportCategories.Acc, primo.AddDays(2), "sorgente irraggiungibile");

        Assert.Equal(primo, await store.GetPrevSuccessAsync(ImportCategories.Acc));
        Assert.Equal(primo.AddDays(1), await store.GetLastSuccessAsync(ImportCategories.Acc));
    }

    [Fact]
    public async Task Il_riallineamento_timbra_ogni_aeroporto_nominato_dalla_sorgente()
    {
        var repo = new EfStructureEditingRepository(_db);
        await repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        await repo.CreateAirportAsync("LIRR", "LIRA", "Roma Ciampino");

        var prima = DateTime.UtcNow;
        await repo.SyncAirportSourceFieldsAsync(new[]
        {
            new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, null),
        });

        var fiumicino = await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIRF");
        var ciampino = await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIRA");

        // Nominato dalla sorgente: timbrato, anche se nessun campo anagrafico è cambiato.
        Assert.NotNull(fiumicino.LastSeenAtUtc);
        Assert.True(fiumicino.LastSeenAtUtc >= prima);
        // Non nominato: nessun timbro. È esattamente il fatto che ne autorizzerà l'eliminazione.
        Assert.Null(ciampino.LastSeenAtUtc);
    }

    [Fact]
    public async Task Un_giro_che_non_cambia_niente_aggiorna_lo_stesso_il_timbro()
    {
        var repo = new EfStructureEditingRepository(_db);
        await repo.CreateAccAsync("LIRR", "Roma ACC", "LI");
        await repo.CreateAirportAsync("LIRR", "LIRF", "Roma Fiumicino");
        var source = new[] { new SourceAirport("LIRF", "Roma Fiumicino", "LIRR", null, null) };

        await repo.SyncAirportSourceFieldsAsync(source);
        var primo = (await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIRF")).LastSeenAtUtc;

        // Il secondo giro non cambia nessun campo anagrafico (ritorna 0) ma la conferma è un fatto suo.
        Assert.Equal(0, await repo.SyncAirportSourceFieldsAsync(source));
        var secondo = (await _db.Airports.AsNoTracking().SingleAsync(a => a.Icao == "LIRF")).LastSeenAtUtc;

        Assert.NotNull(secondo);
        Assert.True(secondo >= primo);
    }
}
