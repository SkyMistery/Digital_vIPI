using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il confine fra l'archivio mondiale e i conti della divisione, contro un database vero.
///
/// <para>Dal 28 agosto 2026 il poller registra <b>tutte</b> le postazioni ATC aperte. Il rischio della
/// modifica non è scrivere le righe nuove — quello si vede subito — ma che una lettura dimenticata cominci
/// a contare il pianeta come se fosse la divisione: ore, classifica, copertura degli scali. Questi test
/// esistono per quella dimenticanza, ed è il motivo per cui ce n'è uno per ogni lettura che conta.</para>
/// </summary>
public class ArchivioAtcMondialeTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private static readonly DateTimeOffset T0 = new(2026, 8, 20, 18, 0, 0, TimeSpan.Zero);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    private async Task Sessione(long id, int vid, string callsign, bool fuori, int secondi = 3600,
        int giorniFa = 0, bool aperta = false, int movimenti = 0)
    {
        var inizio = T0.AddDays(-giorniFa);
        _db.AtcSessions.Add(new AtcSession
        {
            SessionId = id, UserId = vid, Callsign = callsign, Position = callsign.Split('_').Last(),
            StartUtc = inizio.UtcDateTime,
            EndUtc = aperta ? null : inizio.AddSeconds(secondi).UtcDateTime,
            DurationSeconds = secondi, Source = AtcSessionSource.Live, ShiftKey = id,
            MovementCount = movimenti, IsOutsideDivision = fuori,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private static (DateTimeOffset Da, DateTimeOffset A) Anno => (T0.AddDays(-366), T0.AddDays(1));

    // ---------------------------------------------------------------- i conti restano della divisione

    [Fact]
    public async Task Le_ore_della_divisione_NON_contano_il_resto_del_mondo()
    {
        await Sessione(1, 704798, "LIRF_TWR", fuori: false, secondi: 3600);
        await Sessione(2, 111111, "EDDF_TWR", fuori: true, secondi: 36000);
        await Sessione(3, 222222, "KJFK_TWR", fuori: true, secondi: 36000);

        var t = await new EfAtcStatsQueries(_db).TotalsAsync(null, Anno.Da, Anno.A);

        Assert.Equal(1, t.Sessions);
        Assert.Equal(3600, t.Seconds);
    }

    [Fact]
    public async Task La_classifica_non_mette_in_gara_i_controllori_del_mondo()
    {
        await Sessione(1, 704798, "LIRF_TWR", fuori: false);
        await Sessione(2, 111111, "EDDF_TWR", fuori: true, secondi: 36000);

        var rank = await new EfAtcStatsQueries(_db).RankAsync(704798, Anno.Da, Anno.A);

        // Primo su UNO, non su due: il tedesco non è in classifica, quindi non è nemmeno nel denominatore.
        Assert.Equal(1, rank.Position);
        Assert.Equal(1, rank.Total);
    }

    [Fact]
    public async Task L_inizio_dell_archivio_lo_dice_la_divisione()
    {
        // Il mondo entra in archivio SOLO dal 28 agosto 2026, quindi una riga straniera più vecchia non
        // esiste; ma se la lettura la guardasse, la data d'inizio si sposterebbe indietro senza motivo.
        await Sessione(1, 704798, "LIRF_TWR", fuori: false, giorniFa: 10);
        await Sessione(2, 111111, "EDDF_TWR", fuori: true, giorniFa: 300);

        var inizio = await new EfAtcStatsQueries(_db).ArchiveStartAsync(null);

        Assert.Equal(T0.AddDays(-10), inizio);
    }

    [Fact]
    public async Task La_copertura_degli_scali_ignora_gli_aeroporti_esteri()
    {
        // ⚠️ Questa è la lettura più insidiosa: il callsign di un aeroporto estero è ben formato, quindi
        // senza il filtro nascerebbero righe di copertura per scali che non sono nostri.
        await Sessione(1, 704798, "LIRF_TWR", fuori: false);
        await Sessione(2, 111111, "EDDF_TWR", fuori: true);

        var aperture = await new EfAirportTrafficRollupStore(_db).AtcOpeningsAsync(Anno.Da, Anno.A);

        var una = Assert.Single(aperture);
        Assert.Equal("LIRF", una.Icao);
    }

    // ------------------------------------------------- la potatura: stessa scadenza, un solo riassunto

    [Fact]
    public async Task Oltre_i_dodici_mesi_si_pota_tutto_ma_si_riassume_solo_la_divisione()
    {
        await Sessione(1, 704798, "LIRF_TWR", fuori: false, giorniFa: 400, movimenti: 7);
        await Sessione(2, 111111, "EDDF_TWR", fuori: true, giorniFa: 400, movimenti: 99);

        var tolte = await new EfAtcTrafficStore(_db)
            .RollupAndPruneSessionsAsync(T0.AddDays(-366), batch: 100);

        Assert.Equal(2, tolte);
        Assert.Empty(await _db.AtcSessions.ToListAsync());

        // Il riassunto mensile è la memoria lunga delle ore ITALIANE: il tedesco se ne va senza lasciare
        // niente, o la classifica di due anni fa si troverebbe dentro mezzo pianeta.
        var riassunto = Assert.Single(await _db.AtcMonthRollups.ToListAsync());
        Assert.Equal("LIRF_TWR", riassunto.Callsign);
        Assert.Equal(7, riassunto.TrafficMoved);
    }

    // ---------------------------------------------------------------------- la lettura dell'archivio

    [Fact]
    public async Task La_fetta_scelta_decide_cosa_si_vede()
    {
        await Sessione(1, 704798, "LIRF_TWR", fuori: false);
        await Sessione(2, 111111, "EDDF_TWR", fuori: true);

        var q = new EfAtcArchiveQueries(_db);

        Assert.Equal(2, (await q.SearchAsync(new AtcArchiveFilter(Scope: AtcArchiveScope.All))).Total);
        Assert.Equal("EDDF_TWR", Assert.Single(
            (await q.SearchAsync(new AtcArchiveFilter(Scope: AtcArchiveScope.World))).Rows).Callsign);
        Assert.Equal("LIRF_TWR", Assert.Single(
            (await q.SearchAsync(new AtcArchiveFilter(Scope: AtcArchiveScope.Division))).Rows).Callsign);
    }

    [Fact]
    public async Task Una_sessione_entra_nella_finestra_se_si_SOVRAPPONE_non_se_comincia_dentro()
    {
        // Aperta alle 18:00, chiusa alle 19:00. Chi chiede «cosa c'era alle 18:30» la deve trovare, anche
        // se non è cominciata dentro la finestra: è l'errore classico di questa domanda.
        await Sessione(1, 704798, "LIRF_TWR", fuori: false, secondi: 3600);

        var dentro = await new EfAtcArchiveQueries(_db).SearchAsync(new AtcArchiveFilter(
            From: T0.AddMinutes(30), To: T0.AddMinutes(40)));

        Assert.Equal(1, dentro.Total);
    }

    [Fact]
    public async Task Solo_le_aperte_sono_quelle_senza_chiusura()
    {
        await Sessione(1, 704798, "LIRF_TWR", fuori: false);
        await Sessione(2, 111111, "EDDF_TWR", fuori: true, aperta: true);

        var aperte = await new EfAtcArchiveQueries(_db).SearchAsync(new AtcArchiveFilter(OnlyOpen: true));

        var riga = Assert.Single(aperte.Rows);
        Assert.Equal("EDDF_TWR", riga.Callsign);
        Assert.Null(riga.EndUtc);
        Assert.True(riga.IsOutsideDivision);
    }

    [Fact]
    public async Task Il_tetto_delle_righe_non_mente_sul_totale()
    {
        for (var i = 1; i <= 5; i++)
            await Sessione(i, 704798, "LIRF_TWR", fuori: false, giorniFa: i);

        var pagina = await new EfAtcArchiveQueries(_db).SearchAsync(new AtcArchiveFilter(Limit: 2));

        Assert.Equal(2, pagina.Rows.Count);
        Assert.Equal(5, pagina.Total);

        // ⚠️ E il tetto è DURO: chi chiede diecimila righe ne riceve al massimo MaxRighe.
        var troppe = await new EfAtcArchiveQueries(_db).SearchAsync(new AtcArchiveFilter(Limit: 10_000));
        Assert.Equal(5, troppe.Rows.Count);
    }

    [Fact]
    public async Task Gli_istanti_escono_in_UTC_dichiarato()
    {
        // Senza il Kind la pagina scriverebbe l'ora col fuso del server, e il browser la sposterebbe di nuovo.
        await Sessione(1, 704798, "LIRF_TWR", fuori: false);

        var riga = Assert.Single((await new EfAtcArchiveQueries(_db).SearchAsync(new AtcArchiveFilter())).Rows);

        Assert.Equal(TimeSpan.Zero, riga.StartUtc.Offset);
        Assert.Equal(T0, riga.StartUtc);
    }
}
