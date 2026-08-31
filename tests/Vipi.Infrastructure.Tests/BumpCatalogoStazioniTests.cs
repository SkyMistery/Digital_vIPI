using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Chi scrive un <see cref="Acc"/> o un <see cref="Airport"/> deve far invecchiare il catalogo, <b>sempre</b>.
///
/// <para>🔴 <b>Perché la spinta è finita in un intercettore.</b> Ci si è provati chiamando
/// <c>IStationCatalogVersion.Bump()</c> dai servizi, e il 31 agosto 2026 il conto era: <b>quattro</b>
/// chiamate contro <b>undici</b> posti che scrivono quelle due tabelle. Mancava in <c>CreateAcc</c>,
/// <c>DeleteAcc</c>, <c>CreateAirport</c>, <c>DeleteAirport</c>, <c>MoveAirport</c>,
/// <c>SetAirportHidden</c>, in tutta la catena di eliminazione e nella scrittura delle coordinate
/// dell'aeroporto.</para>
///
/// <para>⚠️ <b>Nessuno se n'era accorto</b>, e la ragione è istruttiva: la copia era <c>scoped</c>, quindi
/// una richiesta SSR ne apriva una nuova ogni volta e il dato vecchio durava un istante. Da quando la copia
/// è di <b>processo</b> (<see cref="CatalogoStazioni"/>) lo stesso buco varrebbe «finché qualcuno non
/// riavvia»: un amministratore che crea un ACC non lo vedrebbe comparire, né lui né nessun altro. Per questo
/// la spinta sta dove avviene la scrittura, che è un posto solo.</para>
/// </summary>
public sealed class BumpCatalogoStazioniTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"vipi-bump-{Guid.NewGuid():N}.db");
    private readonly StationCatalogVersion _versione = new();

    private VipiDbContext Contesto() =>
        new(new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .AddInterceptors(new BumpCatalogoStazioniInterceptor(_versione))
            .Options);

    public BumpCatalogoStazioniTests()
    {
        using var db = Contesto();
        db.Database.EnsureCreated();
    }

    private async Task<int> AccDiProva(string codice)
    {
        using var db = Contesto();
        var acc = new Acc { Code = codice, Name = codice, CountryPrefix = codice[..2] };
        db.Accs.Add(acc);
        await db.SaveChangesAsync();
        return acc.Id;
    }

    [Fact]
    public async Task Creare_un_acc_fa_invecchiare_il_catalogo()
    {
        var prima = _versione.Current;
        await AccDiProva("LIBB");
        Assert.NotEqual(prima, _versione.Current);
    }

    [Fact]
    public async Task Creare_un_aeroporto_fa_invecchiare_il_catalogo()
    {
        var accId = await AccDiProva("LIRR");

        var prima = _versione.Current;
        using (var db = Contesto())
        {
            db.Airports.Add(new Airport { AccId = accId, Icao = "LIRF", Name = "Fiumicino" });
            await db.SaveChangesAsync();
        }

        Assert.NotEqual(prima, _versione.Current);
    }

    /// <summary>
    /// ⚠️ Un <c>UPDATE</c> conta quanto un <c>INSERT</c>: nella mappa degli aeroporti stanno anche quota,
    /// variazione magnetica, IATA, coordinate e i due segni militari. Un filtro sul solo inserimento
    /// avrebbe lasciato fuori proprio il giro notturno, che è quello che quei campi li riscrive.
    /// </summary>
    [Fact]
    public async Task Modificare_l_anagrafica_di_un_aeroporto_fa_invecchiare_il_catalogo()
    {
        var accId = await AccDiProva("LIMM");
        using (var db = Contesto())
        {
            db.Airports.Add(new Airport { AccId = accId, Icao = "LIML", Name = "Linate" });
            await db.SaveChangesAsync();
        }

        var prima = _versione.Current;
        using (var db = Contesto())
        {
            var apt = await db.Airports.FirstAsync(a => a.Icao == "LIML");
            apt.ElevationFt = 353;
            await db.SaveChangesAsync();
        }

        Assert.NotEqual(prima, _versione.Current);
    }

    [Fact]
    public async Task Eliminare_un_aeroporto_fa_invecchiare_il_catalogo()
    {
        var accId = await AccDiProva("LIPP");
        using (var db = Contesto())
        {
            db.Airports.Add(new Airport { AccId = accId, Icao = "LIPZ", Name = "Venezia" });
            await db.SaveChangesAsync();
        }

        var prima = _versione.Current;
        using (var db = Contesto())
        {
            db.Airports.Remove(await db.Airports.FirstAsync(a => a.Icao == "LIPZ"));
            await db.SaveChangesAsync();
        }

        Assert.NotEqual(prima, _versione.Current);
    }

    /// <summary>
    /// E chi non tocca quelle due tabelle <b>non</b> spinge. Una spinta di troppo costa solo una rilettura,
    /// ma se spingesse ogni salvataggio la copia non varrebbe più niente: si rileggerebbe a ogni scrittura
    /// del sito, che è la situazione da cui si veniva.
    /// </summary>
    [Fact]
    public async Task Scrivere_altro_non_fa_invecchiare_il_catalogo()
    {
        var prima = _versione.Current;

        using (var db = Contesto())
        {
            db.Documents.Add(new Document { Title = "Prova", Type = DocumentType.Vipi, LastUpdatedAiracCycle = "2609" });
            await db.SaveChangesAsync();
        }

        Assert.Equal(prima, _versione.Current);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { /* file di prova: se resta, pazienza */ }
    }
}
