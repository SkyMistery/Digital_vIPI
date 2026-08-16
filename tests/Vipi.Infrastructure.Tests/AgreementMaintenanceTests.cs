using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
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
/// </summary>
public class AgreementMaintenanceTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfTransferRepository _flows = default!;
    private EfAgreementRepository _agreements = default!;
    private EfAgreementMaintenance _travaso = default!;
    private int _neId, _tsId, _ftwrId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        await RomaStructureSeed.SeedAsync(_db);

        _flows = new EfTransferRepository(_db);
        _agreements = new EfAgreementRepository(_db);
        _travaso = new EfAgreementMaintenance(_db, _flows, new EfImportStateStore(_db));

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
        var flowId = await _flows.AddFlowAsync("LIRR", Flow(TransferFlowKind.Arrival, "LIRF"));
        await _flows.AddPointAsync("LIRR", flowId, Point("VALMA", 130, _ftwrId));
        await _flows.AddPointAsync("LIRR", flowId, Point("ELKAP", 150, _tsId));

        await _travaso.MigrateFlowsToAgreementsAsync();

        var accordi = await _agreements.ListByAccAsync("LIRR");
        Assert.Equal(2, accordi.Count);
        Assert.Equal(new[] { "LIRF_TWR", "LIRR_TS_CTR" },
            accordi.Select(a => a.Parties.Single(p => p.Side == AgreementSide.B).Callsign).OrderBy(x => x));
    }

    [Fact]
    public async Task Il_gruppo_di_varianti_sopravvive_al_travaso()
    {
        var flowId = await _flows.AddFlowAsync("LIRR", Flow(TransferFlowKind.Arrival, "LIRF"));
        var first = await _flows.AddPointAsync("LIRR", flowId, Point("BIRSU", 150, _ftwrId));
        var alt = await _flows.AddAlternativeAsync("LIRR", first);
        await _flows.AddExceptionAsync("LIRR", alt);

        await _travaso.MigrateFlowsToAgreementsAsync();

        var a = Assert.Single(await _agreements.ListByAccAsync("LIRR"));
        Assert.Equal(3, a.Clauses.Count);
        Assert.Single(a.Clauses.Select(c => c.VariantGroup).Distinct());
        // La struttura, non solo il numero di righe: profondita' e ordine sono cio' che l'outline SIGNIFICA.
        Assert.Equal(new[] { 0, 0, 1 }, a.Clauses.OrderBy(c => c.Order).Select(c => c.VariantDepth));
    }

    [Fact]
    public async Task Aeroporti_diversi_con_le_stesse_righe_diventano_un_accordo_solo()
    {
        foreach (var icao in new[] { "LIRF", "LIRA" })
        {
            var id = await _flows.AddFlowAsync("LIRR", Flow(TransferFlowKind.Arrival, icao));
            await _flows.AddPointAsync("LIRR", id, Point("ASPIR", 210, _tsId));
        }

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
        var arrivi = await _flows.AddFlowAsync("LIRR", Flow(TransferFlowKind.Arrival, "LIRF"));
        await _flows.AddPointAsync("LIRR", arrivi, Point("VALMA", 130, _ftwrId));
        await _flows.AddPointAsync("LIRR", arrivi, Point("ELKAP", 150, _tsId));

        var gemello = await _flows.AddFlowAsync("LIRR", Flow(TransferFlowKind.Arrival, "LIRA"));
        await _flows.AddPointAsync("LIRR", gemello, Point("VALMA", 130, _ftwrId));

        var sorvoli = await _flows.AddFlowAsync("LIRR", Flow(TransferFlowKind.Overflight, null));
        foreach (var cop in new[] { "TIGRA", "NOSTO", "LATAN" })
            await _flows.AddPointAsync("LIRR", sorvoli, Point(cop, 300, _tsId));

        var varianti = await _flows.AddFlowAsync("LIRR", Flow(TransferFlowKind.Departure, "LIRF"));
        var capofila = await _flows.AddPointAsync("LIRR", varianti, Point("BIRSU", 90, _tsId));
        await _flows.AddAlternativeAsync("LIRR", capofila);

        await _flows.AddFlowAsync("LIRR", Flow(TransferFlowKind.Vfr, null));   // intestazione senza righe
    }

    private TransferFlowInput Flow(TransferFlowKind kind, string? icao) => new()
    {
        OwningSectorId = _neId, Kind = kind, AirportIcao = icao,
    };

    private static TransferPointInput Point(string cop, int level, int? next) => new()
    {
        Cop = cop, LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = LevelConstraint.AtOrBelow,
        NextSectorId = next,
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
