using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Le piste di più aeroporti in una lettura sola.
///
/// <para><b>Perché.</b> L'elenco degli aeroporti di una ACC mostra la pista consigliata accanto a ogni
/// scalo, e per calcolarla caricava il profilo INTERO di ogni aeroporto, uno alla volta: livelli di
/// transizione, SID, link-frequenze — roba che quell'elenco non guarda — con <b>otto query a testa, in
/// fila</b>. Contate il 27 agosto 2026: 36 query per una pagina di tre righe, e 120 per una ACC con
/// quindici scali. Adesso sono due, e restano due.</para>
/// </summary>
public class PisteMassiveTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly ContaComandi _conta = new();
    private VipiDbContext _db = default!;
    private EfAirportRepository _repo = default!;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>()
            .UseSqlite(_conn).AddInterceptors(_conta).Options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfAirportRepository(_db, new EfMediaMaintenance(_db));

        var acc = new Acc { Code = "LIBB", Name = "Brindisi", CountryPrefix = "LI" };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync();

        foreach (var (icao, piste) in new[] { ("LIBC", new[] { "05", "23" }), ("LIBD", new[] { "14" }), ("LIBR", Array.Empty<string>()) })
        {
            var a = new Airport { Icao = icao, Name = icao, AccId = acc.Id };
            _db.Airports.Add(a);
            await _db.SaveChangesAsync();
            for (var i = 0; i < piste.Length; i++)
                _db.AirportRunways.Add(new AirportRunway { AirportId = a.Id, Ident = piste[i], Order = i });
        }
        await _db.SaveChangesAsync();

        var libc = await _db.Airports.SingleAsync(x => x.Icao == "LIBC");
        _db.AirportRunwayRules.Add(new AirportRunwayRule { AirportId = libc.Id, Name = "notte", DepRunways = "05", ArrRunways = "05", Order = 0 });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Rende_piste_e_regole_di_tutti_gli_aeroporti_chiesti()
    {
        var esito = await _repo.ListRunwayDataAsync(new[] { "LIBC", "LIBD", "LIBR" });

        Assert.Equal(3, esito.Count);
        Assert.Equal(new[] { "05", "23" }, esito["LIBC"].Runways.Select(r => r.Ident));
        Assert.Single(esito["LIBC"].Rules);
        Assert.Single(esito["LIBD"].Runways);
        Assert.Empty(esito["LIBR"].Runways);
        Assert.Empty(esito["LIBR"].Rules);
    }

    /// <summary>
    /// <b>L'invariante che vale il metodo.</b> Il numero di query non deve dipendere dal numero di
    /// aeroporti: è esattamente la proprietà che il codice di prima non aveva.
    ///
    /// <para>⚠️ Si conta con un intercettore di comandi, che è l'unica cosa che conta davvero le query.
    /// Il primo giro di questo test si appoggiava all'evento <c>SavingChanges</c> — che in lettura non
    /// scatta mai — e sarebbe stato verde confrontando zero con zero. È il terzo test di questa serie a
    /// cadere nello stesso modo: quando una misura viene comoda, va guardata due volte.</para>
    /// </summary>
    [Fact]
    public async Task Il_numero_di_query_non_cresce_col_numero_di_aeroporti()
    {
        _conta.Azzera();
        await _repo.ListRunwayDataAsync(new[] { "LIBC" });
        var uno = _conta.Totale;

        _conta.Azzera();
        await _repo.ListRunwayDataAsync(new[] { "LIBC", "LIBD", "LIBR" });
        var tre = _conta.Totale;

        Assert.True(uno > 0, "l'intercettore non ha visto nessuna query: non sta contando niente.");
        Assert.Equal(uno, tre);
        Assert.True(tre <= 3, $"{tre} query per tre aeroporti: dovrebbero bastarne tre (aeroporti, piste, regole).");
    }

    /// <summary>Un ICAO che non esiste non compare, e non fa saltare gli altri.</summary>
    [Fact]
    public async Task Un_icao_sconosciuto_semplicemente_non_compare()
    {
        var esito = await _repo.ListRunwayDataAsync(new[] { "LIBC", "ZZZZ" });

        Assert.True(esito.ContainsKey("LIBC"));
        Assert.False(esito.ContainsKey("ZZZZ"));
    }

    /// <summary>Minuscole, spazi, elenco vuoto: la porta non deve rompersi su come la si chiama.</summary>
    [Fact]
    public async Task Tollera_minuscole_spazi_ed_elenco_vuoto()
    {
        Assert.Empty(await _repo.ListRunwayDataAsync(Array.Empty<string>()));
        Assert.True((await _repo.ListRunwayDataAsync(new[] { " libc " })).ContainsKey("LIBC"));
    }

    /// <summary>Conta i comandi che partono davvero verso il database.</summary>
    private sealed class ContaComandi : DbCommandInterceptor
    {
        public int Totale;
        public void Azzera() => Totale = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Totale++;
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Totale++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
