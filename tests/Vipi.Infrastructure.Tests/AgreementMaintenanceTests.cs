using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.Seed;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il **travaso** dei flussi negli accordi, sul database.
/// <para>La regola di conversione è già provata sui 78 punti veri senza database
/// (<c>FlowsToAgreementsTests</c>); qui si prova ciò che solo un database può dire: che il giro completo —
/// flussi scritti, travasati, riletti come accordi e riespansi — restituisce le stesse righe, e che la passata
/// non si ripete.</para>
/// <para>I flussi si scrivono <b>come entità</b>, non con una API: quella non esiste più, ed è giusto così —
/// i flussi sono dati storici da leggere una volta, non un modello su cui si scrive.</para>
/// </summary>
public class AgreementMaintenanceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfLegacyFlowReader _flows = default!;
    private EfAgreementRepository _agreements = default!;
    private EfAgreementMaintenance _travaso = default!;
    private int _accId, _neId, _tsId, _ftwrId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        _flows = new EfLegacyFlowReader(_db);
        _agreements = new EfAgreementRepository(_db);
        _travaso = new EfAgreementMaintenance(_db, _flows, new EfImportStateStore(_db));

        _accId = (await _db.Accs.FirstAsync(a => a.Code == "LIRR")).Id;
        var sectors = await _db.Sectors.ToListAsync();
        _neId = sectors.First(s => s.Callsign == "LIRR_NE_CTR").Id;
        _tsId = sectors.First(s => s.Callsign == "LIRR_TS_CTR").Id;
        _ftwrId = sectors.First(s => s.Callsign == "LIRF_TWR").Id;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _conn.DisposeAsync();
    }

    [Fact]
    public async Task Il_travaso_conserva_le_righe_derivate()
    {
        await SeedFlowsAsync();
        var prima = Signature(await _flows.ListFlowsByAccAsync("LIRR"));

        await _travaso.MigrateFlowsToAgreementsAsync();

        var dopo = Signature(AgreementExpansion.Expand(await _agreements.ListByAccAsync("LIRR")));
        Assert.Equal(prima, dopo);
    }

    [Fact]
    public async Task Il_travaso_non_si_ripete()
    {
        await SeedFlowsAsync();

        var primo = await _travaso.MigrateFlowsToAgreementsAsync();
        var secondo = await _travaso.MigrateFlowsToAgreementsAsync();

        Assert.True(primo > 0);
        // Non «gli accordi ci sono gia'» ma il segnaposto: chi li cancellasse tutti da editor si ritroverebbe
        // l'archivio vecchio rimesso dentro al riavvio, senza nessuna traccia del perche'.
        Assert.Equal(0, secondo);
        Assert.Equal(primo, (await _agreements.ListByAccAsync("LIRR")).Count);
    }

    [Fact]
    public async Task Un_flusso_con_riceventi_diversi_diventa_due_accordi_anche_sul_database()
    {
        Add(Flow(TransferFlowKind.Arrival, "LIRF",
            Point("VALMA", 130, _ftwrId, 1),
            Point("ELKAP", 150, _tsId, 2)));
        await _db.SaveChangesAsync();

        await _travaso.MigrateFlowsToAgreementsAsync();

        var accordi = await _agreements.ListByAccAsync("LIRR");
        Assert.Equal(2, accordi.Count);
        Assert.Equal(new[] { "LIRF_TWR", "LIRR_TS_CTR" },
            accordi.Select(a => a.Parties.Single(p => p.Side == AgreementSide.B).Callsign).OrderBy(x => x));
    }

    [Fact]
    public async Task Il_gruppo_di_varianti_sopravvive_al_travaso()
    {
        // Tre righe dello stesso gruppo: due alternative pari-grado e un'eccezione della seconda. È la forma che
        // il travaso non deve appiattire — e che la fusione dei punti aveva sciolto, prima che questo test la
        // cogliesse.
        Add(Flow(TransferFlowKind.Arrival, "LIRF",
            Point("BIRSU", 150, _ftwrId, 1, group: 1, depth: 0, runway: "07"),
            Point("BIRSU", 130, _ftwrId, 2, group: 1, depth: 0, runway: "25"),
            Point("BIRSU", 110, _ftwrId, 3, group: 1, depth: 1)));
        await _db.SaveChangesAsync();

        await _travaso.MigrateFlowsToAgreementsAsync();

        var a = Assert.Single(await _agreements.ListByAccAsync("LIRR"));
        Assert.Equal(3, a.Clauses.Count);
        Assert.Single(a.Clauses.Select(c => c.VariantGroup).Distinct());
        // La struttura, non solo il numero di righe: profondità e ordine sono ciò che l'outline SIGNIFICA.
        Assert.Equal(new[] { 0, 0, 1 }, a.Clauses.OrderBy(c => c.Order).Select(c => c.VariantDepth));
    }

    [Fact]
    public async Task Aeroporti_diversi_con_le_stesse_righe_diventano_un_accordo_solo()
    {
        Add(Flow(TransferFlowKind.Arrival, "LIRF", Point("ASPIR", 210, _tsId, 1)));
        Add(Flow(TransferFlowKind.Arrival, "LIRA", Point("ASPIR", 210, _tsId, 1)));
        await _db.SaveChangesAsync();

        await _travaso.MigrateFlowsToAgreementsAsync();

        var a = Assert.Single(await _agreements.ListByAccAsync("LIRR"));
        Assert.Equal(new[] { "LIRF", "LIRA" }, a.Airports.Select(x => x.Icao));
        // E riespanso torna a essere due flussi, uno per aeroporto: il documento non cambia.
        Assert.Equal(2, AgreementExpansion.Expand(new[] { a }).Count);
    }

    // ---- attrezzi ------------------------------------------------------------------------------------

    private async Task SeedFlowsAsync()
    {
        // Un archivio in miniatura coi casi che contano: riceventi misti, aeroporti fondibili, punti
        // consecutivi identici, un gruppo di varianti, un flusso senza righe.
        Add(Flow(TransferFlowKind.Arrival, "LIRF",
            Point("VALMA", 130, _ftwrId, 1),
            Point("ELKAP", 150, _tsId, 2)));
        Add(Flow(TransferFlowKind.Arrival, "LIRA", Point("VALMA", 130, _ftwrId, 1)));
        Add(Flow(TransferFlowKind.Overflight, null,
            Point("TIGRA", 300, _tsId, 1),
            Point("NOSTO", 300, _tsId, 2),
            Point("LATAN", 300, _tsId, 3)));
        Add(Flow(TransferFlowKind.Departure, "LIRF",
            Point("BIRSU", 90, _tsId, 1, group: 1, depth: 0, runway: "16R"),
            Point("BIRSU", 70, _tsId, 2, group: 1, depth: 0, runway: "34L")));
        Add(Flow(TransferFlowKind.Vfr, null));   // intestazione senza righe
        await _db.SaveChangesAsync();
    }

    private void Add(TransferFlow f) => _db.TransferFlows.Add(f);

    private TransferFlow Flow(TransferFlowKind kind, string? icao, params TransferPoint[] points) => new()
    {
        AccId = _accId,
        OwningSectorId = _neId,
        Kind = kind,
        AirportIcao = icao,
        Order = _db.TransferFlows.Local.Count + 1,
        Points = points,
    };

    private static TransferPoint Point(string cop, int level, int next, int order,
        int? group = null, int depth = 0, string? runway = null) => new()
    {
        Cop = cop,
        LevelValue = level,
        LevelUnit = LevelUnit.Fl,
        LevelConstraint = LevelConstraint.AtOrBelow,
        NextSectorId = next,
        ConditionLabel = runway,
        VariantGroup = group,
        VariantDepth = depth,
        Order = order,
    };

    /// <summary>
    /// Cosa dev'essere identico: le RIGHE, non i flussi che le contengono. L'ordine dei flussi cambia apposta
    /// (aeroporti fusi, riceventi separati), quindi il confronto è sull'insieme ordinato delle righe con tutto
    /// ciò che portano — mittente, aeroporto, tipo, punto, livello, ricevente, condizione, posizione
    /// nell'outline.
    /// </summary>
    private static string Signature(IReadOnlyList<TransferFlowRow> flows) =>
        string.Join("\n", flows
            .SelectMany(f => f.Points.Select(p => string.Join('|',
                f.OwningSectorCallsign, f.Kind, f.AirportIcao ?? "-", p.Cop, p.LevelText,
                p.NextSectorCallsign ?? "-", p.ConditionDisplay ?? "-",
                p.VariantGroup?.ToString() ?? "-", p.VariantDepth, p.IsGroupWide)))
            .OrderBy(x => x, StringComparer.Ordinal));
}
