using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// L'eliminazione sul database vero: la lettura dei <b>fatti</b> e l'esecuzione delle <b>mosse</b>.
///
/// <para>Le politiche stanno in <c>DeletionRules</c> e sono provate senza database in
/// <c>DeletionRulesTests</c>. Qui si prova l'altra metà: che le query trovino davvero chi cita cosa, e che
/// la transazione lasci l'archivio nello stato che il piano aveva promesso — figli riappesi al nonno prima
/// del <c>DELETE</c> (la FK sul padre è <c>Restrict</c>), righe di catalogo tolte insieme alla proiezione,
/// audit scritto col nome dentro.</para>
/// </summary>
public class DeletionRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDeletionRepository _repo = default!;

    private Acc _lirr = default!;
    private Airport _lirf = default!;
    private Sector _ctr = default!, _app = default!, _twr = default!, _gnd = default!;
    private Document _accDoc = default!, _scaloDoc = default!;

    private static readonly DateTime Penultimo = DateTime.UtcNow.AddDays(-1);
    private static readonly DateTime Vecchio = DateTime.UtcNow.AddDays(-5);

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();
        _repo = new EfDeletionRepository(_db, new EfUnitOfWork(_db));

        _lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI", ImportedAtUtc = Vecchio };
        _db.Accs.Add(_lirr);

        _accDoc = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma ACC", LastUpdatedAiracCycle = "2608" };
        _scaloDoc = new Document { Type = DocumentType.Vipi, Title = "vIPI Fiumicino", LastUpdatedAiracCycle = "2608" };
        _db.Documents.AddRange(_accDoc, _scaloDoc);
        await _db.SaveChangesAsync();

        _lirf = new Airport { Icao = "LIRF", Name = "Fiumicino", Acc = _lirr, LastSeenAtUtc = Vecchio, DocumentId = _scaloDoc.Id };
        _db.Airports.Add(_lirf);
        await _db.SaveChangesAsync();

        // Catalogo: il timbro che conta per l'eliminazione è QUESTO, non quello della proiezione.
        _db.AccSectors.Add(new AccSector { ComposePosition = "LIRR_CTR", CenterId = "LIRR", Position = "CTR", ImportedAtUtc = Vecchio });
        _db.AirportSectors.AddRange(
            new AirportSector { ComposePosition = "LIRF_APP", AirportIcao = "LIRF", AccCode = "LIRR", Position = "APP", ImportedAtUtc = Vecchio },
            new AirportSector { ComposePosition = "LIRF_TWR", AirportIcao = "LIRF", AccCode = "LIRR", Position = "TWR", ImportedAtUtc = Vecchio },
            new AirportSector { ComposePosition = "LIRF_GND", AirportIcao = "LIRF", AccCode = "LIRR", Position = "GND", ImportedAtUtc = Vecchio });

        _ctr = Sec("LIRR_CTR", SectorType.Ctr, SectorKind.Acc, documento: _accDoc.Id);
        _db.Sectors.Add(_ctr);
        await _db.SaveChangesAsync();

        _app = Sec("LIRF_APP", SectorType.App, SectorKind.Airport, padre: _ctr.Id, aeroporto: _lirf.Id);
        _db.Sectors.Add(_app);
        await _db.SaveChangesAsync();

        _twr = Sec("LIRF_TWR", SectorType.Twr, SectorKind.Airport, padre: _app.Id, aeroporto: _lirf.Id);
        _gnd = Sec("LIRF_GND", SectorType.Gnd, SectorKind.Airport, padre: _app.Id, aeroporto: _lirf.Id);
        _db.Sectors.AddRange(_twr, _gnd);
        await _db.SaveChangesAsync();

        Sector Sec(string cs, SectorType t, SectorKind k, int? documento = null, int? padre = null, int? aeroporto = null) =>
            new()
            {
                Acc = _lirr, Callsign = cs, Name = cs, Type = t, Kind = k, DocumentId = documento,
                ParentSectorId = padre, AirportId = aeroporto, AirportIcao = aeroporto is null ? null : "LIRF",
                IsProjected = true, IsActive = true, ImportedAtUtc = Vecchio,
            };
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private async Task<DeletionPlan> PianoSettoreAsync(int id) =>
        DeletionRules.PerSettore(await _repo.SectorFactsAsync(id) ?? throw new Xunit.Sdk.XunitException("fatti mancanti"), Penultimo);

    // ── I fatti ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_fatti_di_un_settore_portano_figli_padre_e_timbro_di_catalogo()
    {
        var f = await _repo.SectorFactsAsync(_app.Id);

        Assert.NotNull(f);
        Assert.Equal("LIRF_APP", f!.Callsign);
        Assert.Equal("LIRR_CTR", f.ParentCallsign);
        Assert.Equal(2, f.Figli.Count);
        Assert.True(f.ImportedAtUtc is { } t && Math.Abs((t - Vecchio).TotalSeconds) < 1);
        Assert.False(f.CatalogoManuale);
    }

    [Fact]
    public async Task Una_riga_di_catalogo_a_mano_si_riconosce()
    {
        var riga = await _db.AccSectors.SingleAsync(x => x.ComposePosition == "LIRR_CTR");
        riga.IsManual = true;
        await _db.SaveChangesAsync();

        var f = await _repo.SectorFactsAsync(_ctr.Id);
        Assert.True(f!.CatalogoManuale);
    }

    [Fact]
    public async Task Un_documento_ancorato_altrove_risulta_ancora_agganciato()
    {
        // Un secondo settore porta lo stesso documento: togliendo il primo, il documento resta raggiungibile.
        var altro = new Sector
        {
            Acc = _lirr, Callsign = "LIRR_N_CTR", Name = "Roma Nord", Type = SectorType.Ctr,
            Kind = SectorKind.Acc, DocumentId = _accDoc.Id, IsProjected = true, IsActive = true,
        };
        _db.Sectors.Add(altro);
        await _db.SaveChangesAsync();

        var f = await _repo.SectorFactsAsync(_ctr.Id);
        Assert.True(Assert.Single(f!.Documenti).RestaAncorato);
    }

    [Fact]
    public async Task Un_documento_appeso_solo_a_questo_settore_non_resta_agganciato()
    {
        var f = await _repo.SectorFactsAsync(_ctr.Id);
        var d = Assert.Single(f!.Documenti);

        Assert.True(d.AncoraQui);
        Assert.False(d.RestaAncorato);
        Assert.Equal("vIPI Roma ACC", d.Titolo);
    }

    [Fact]
    public async Task Un_blocco_che_cita_il_settore_arriva_col_suo_documento()
    {
        var (blocco, _) = await BloccoAsync(_accDoc, scope: _gnd.Id);

        var f = await _repo.SectorFactsAsync(_gnd.Id);
        var d = Assert.Single(f!.Documenti);
        var b = Assert.Single(d.Blocchi);

        Assert.Equal(blocco.Id, b.BlockId);
        Assert.True(b.Scope);
        Assert.False(b.Estremo);
    }

    [Fact]
    public async Task Un_accordo_di_coordinamento_arriva_con_i_due_callsign()
    {
        _db.CoordinationAgreements.Add(new CoordinationAgreement
        {
            OwnerAccId = _lirr.Id, SideASectorId = _ctr.Id, SideBSectorId = _app.Id,
        });
        await _db.SaveChangesAsync();

        var f = await _repo.SectorFactsAsync(_app.Id);
        Assert.Contains("LIRR_CTR", Assert.Single(f!.Accordi).Etichetta);
    }

    // ── L'esecuzione ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Eliminando_un_settore_i_figli_passano_al_nonno()
    {
        var piano = await PianoSettoreAsync(_app.Id);
        Assert.True(piano.Eliminabile);

        await _repo.ApplyAsync(piano.Azioni, actorUserId: 7);

        Assert.Null(await _db.Sectors.AsNoTracking().FirstOrDefaultAsync(s => s.Id == _app.Id));
        var twr = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _twr.Id);
        var gnd = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _gnd.Id);
        Assert.Equal(_ctr.Id, twr.ParentSectorId);
        Assert.Equal(_ctr.Id, gnd.ParentSectorId);
    }

    [Fact]
    public async Task Eliminando_un_settore_sparisce_anche_la_sua_riga_di_catalogo()
    {
        // Togliere solo la proiezione lo farebbe tornare al primo sync: chi guarda lo vedrebbe risorgere.
        var piano = await PianoSettoreAsync(_gnd.Id);
        await _repo.ApplyAsync(piano.Azioni, actorUserId: 7);

        Assert.False(await _db.AirportSectors.AnyAsync(x => x.ComposePosition == "LIRF_GND"));
    }

    [Fact]
    public async Task L_audit_dell_eliminazione_porta_il_nome_non_solo_l_id()
    {
        var piano = await PianoSettoreAsync(_gnd.Id);
        await _repo.ApplyAsync(piano.Azioni, actorUserId: 7);

        var riga = await _db.AuditLogs.AsNoTracking()
            .SingleAsync(a => a.EntityType == "Sector" && a.Action == AuditAction.Delete);
        Assert.Equal(7, riga.UserId);
        Assert.Contains("LIRF_GND", riga.DetailsJson);
    }

    [Fact]
    public async Task Un_blocco_estremo_muore_e_uno_di_solo_ambito_resta_sganciato()
    {
        var (estremo, _) = await BloccoAsync(_accDoc, from: _gnd.Id, to: _twr.Id);
        var (ambito, _) = await BloccoAsync(_accDoc, scope: _gnd.Id);

        var piano = await PianoSettoreAsync(_gnd.Id);
        await _repo.ApplyAsync(piano.Azioni, actorUserId: 7);

        Assert.False(await _db.ContentBlocks.AnyAsync(b => b.Id == estremo.Id));
        var superstite = await _db.ContentBlocks.AsNoTracking().SingleAsync(b => b.Id == ambito.Id);
        Assert.Null(superstite.ScopeSectorId);
    }

    [Fact]
    public async Task Una_parte_di_vloa_sparisce_col_settore()
    {
        var vloa = new Document { Type = DocumentType.Vloa, Title = "vLOA LIRR ↔ LFMM", LastUpdatedAiracCycle = "2608" };
        _db.Documents.Add(vloa);
        await _db.SaveChangesAsync();
        _db.DocumentParties.AddRange(
            new DocumentParty { DocumentId = vloa.Id, SectorId = _gnd.Id, Role = PartyRole.Home },
            new DocumentParty { DocumentId = vloa.Id, SectorId = _ctr.Id, Role = PartyRole.Neighbour });
        await _db.SaveChangesAsync();

        var piano = await PianoSettoreAsync(_gnd.Id);
        Assert.True(piano.Eliminabile);
        await _repo.ApplyAsync(piano.Azioni, actorUserId: 7);

        Assert.Equal(1, await _db.DocumentParties.CountAsync(p => p.DocumentId == vloa.Id));
        Assert.Contains(vloa.Id, piano.Azioni.DocumentiDaMarcare);
    }

    [Fact]
    public async Task Lo_scalo_porta_via_tutti_i_suoi_settori()
    {
        // Il documento dello scalo bloccherebbe: qui si prova la cascata, quindi lo si toglie prima —
        // che è esattamente l'ordine che la finestra impone all'utente.
        _lirf.DocumentId = null;
        await _db.SaveChangesAsync();

        var f = await _repo.AirportFactsAsync(_lirf.Id);
        var piano = DeletionRules.PerAeroporto(f!, Penultimo, Penultimo);
        Assert.True(piano.Eliminabile);

        await _repo.ApplyAsync(piano.Azioni, actorUserId: 7);

        Assert.False(await _db.Airports.AnyAsync(a => a.Id == _lirf.Id));
        Assert.False(await _db.Sectors.AnyAsync(s => s.AirportIcao == "LIRF"));
        // Il CTR non era dello scalo: resta, e resta radice.
        var ctr = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _ctr.Id);
        Assert.Null(ctr.ParentSectorId);
    }

    [Fact]
    public async Task I_fatti_di_una_acc_contano_settori_e_aeroporti()
    {
        var f = await _repo.AccFactsAsync("LIRR");

        Assert.Equal(4, f!.Settori);
        Assert.Equal(1, f.Aeroporti);
        Assert.False(DeletionRules.PerAcc(f, Penultimo).Eliminabile);
    }

    [Fact]
    public async Task I_fatti_di_un_documento_dicono_chi_lo_perde()
    {
        var f = await _repo.DocumentFactsAsync(_scaloDoc.Id);

        Assert.Equal("vIPI Fiumicino", f!.Titolo);
        Assert.Equal("LIRF", f.AeroportoCheLoPerde);
    }

    /// <summary>Un blocco vero: serve una versione e una sezione, o le FK non reggono.</summary>
    private async Task<(ContentBlock Blocco, DocumentSection Sezione)> BloccoAsync(
        Document doc, int? scope = null, int? from = null, int? to = null)
    {
        // La versione e la sezione si riusano: due blocchi dello stesso documento stanno nella stessa
        // versione, e l'unicità (DocumentId, VersionNumber) non ammette due «versione 1».
        var v = await _db.DocumentVersions.FirstOrDefaultAsync(x => x.DocumentId == doc.Id);
        if (v is null)
        {
            v = new DocumentVersion
            {
                DocumentId = doc.Id, VersionNumber = 1, Status = DocumentStatus.Draft,
                AiracCycle = "2608", CreatedUtc = DateTime.UtcNow,
            };
            _db.DocumentVersions.Add(v);
            await _db.SaveChangesAsync();
        }

        var s = await _db.DocumentSections.FirstOrDefaultAsync(x => x.DocumentVersionId == v.Id);
        if (s is null)
        {
            s = new DocumentSection { DocumentVersionId = v.Id, Title = "Coordinamenti", Order = 1, Depth = 0 };
            _db.DocumentSections.Add(s);
            await _db.SaveChangesAsync();
        }

        var b = new ContentBlock
        {
            DocumentVersionId = v.Id, SectionId = s.Id, Order = 1,
            ScopeSectorId = scope, FromSectorId = from, ToSectorId = to, Body = "prova",
        };
        _db.ContentBlocks.Add(b);
        await _db.SaveChangesAsync();
        return (b, s);
    }
}
