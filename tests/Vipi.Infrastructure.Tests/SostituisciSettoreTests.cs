using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// «Sostituisci con…» (S54, committente, 29 settembre 2026). Il caso che il committente teme: un giorno
/// <c>LIRN_US0_APP</c> diventa <c>LIRR_US0_APP</c>. Se IVAO rinomina la STESSA riga, la rinomina automatica fa già
/// tutto; se invece toglie la vecchia e ne crea una nuova (identità diversa, qui perfino un catalogo diverso), tutto
/// restava sul settore vecchio — accordi, blocchi, figli, il gruppo APP nella vIPI ACC — e il nuovo nasceva nudo.
/// </summary>
public class SostituisciSettoreTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;

    private const string Vecchio = "LIRN_US0_APP";
    private const string Nuovo = "LIRR_US0_APP";

    private Acc _lirr = default!;
    private Sector _ctr = default!, _vecchio = default!, _nuovo = default!, _torre = default!;
    private int _accordoId, _bloccoId, _gruppoId, _vipiAcc;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();

        _lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(_lirr);
        var vipi = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        _db.Documents.Add(vipi);
        _db.Airports.Add(new Airport { Icao = "LIRN", Name = "Napoli", Acc = _lirr });
        await _db.SaveChangesAsync();
        _vipiAcc = vipi.Id;

        // Il vecchio: una postazione d'aeroporto di Napoli, sparita da IVAO (non più attiva), col suo padre scritto.
        _db.AirportSectors.Add(new AirportSector
        {
            IvaoId = 700, ComposePosition = Vecchio, AirportIcao = "LIRN", AccCode = "LIRR", Position = "APP", ParentCallsign = "LIRR_CTR",
        });
        // Il nuovo: un subcenter dell'ACC, identità e catalogo diversi, ancora senza padre.
        _db.AccSectors.Add(new AccSector { IvaoId = 9100, ComposePosition = Nuovo, CenterId = "LIRR", Position = "APP" });

        _ctr = new Sector { Acc = _lirr, Callsign = "LIRR_CTR", Name = "Roma", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true, IsProjected = true, DocumentId = vipi.Id, IsPrimary = true };
        _vecchio = new Sector { Acc = _lirr, Callsign = Vecchio, Name = "Napoli", Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Remotized, IsActive = false, IsProjected = true, ParentSector = _ctr };
        _nuovo = new Sector { Acc = _lirr, Callsign = Nuovo, Name = "Roma US0", Type = SectorType.App, Kind = SectorKind.Acc, ApproachKind = ApproachKind.Remotized, IsActive = true, IsProjected = true };
        _db.Sectors.AddRange(_ctr, _vecchio, _nuovo);
        await _db.SaveChangesAsync();
        _torre = new Sector { Acc = _lirr, Callsign = "LIRN_TWR", Name = "Napoli TWR", Type = SectorType.Twr, Kind = SectorKind.Airport, IsActive = true, IsProjected = true, ParentSectorId = _vecchio.Id };
        _db.Sectors.Add(_torre);

        // Un accordo vecchio ↔ CTR, con una sezione orientata: dopo la sostituzione i lati possono scambiarsi.
        var accordo = new CoordinationAgreement
        {
            OwnerAccId = _lirr.Id, SideASectorId = Math.Min(_vecchio.Id, _ctr.Id), SideBSectorId = Math.Max(_vecchio.Id, _ctr.Id),
        };
        accordo.Sections.Add(new AgreementSection
        {
            Order = 1, Direction = accordo.SideASectorId == _vecchio.Id ? AgreementDirection.AtoB : AgreementDirection.BtoA,
        });
        _db.CoordinationAgreements.Add(accordo);

        // La vIPI di Roma: un gruppo APP che elenca il vecchio, e un blocco di coordinamento che lo cita per numero.
        var versione = new DocumentVersion { DocumentId = vipi.Id, VersionNumber = 1, AiracCycle = "2610" };
        _db.DocumentVersions.Add(versione);
        await _db.SaveChangesAsync();
        _accordoId = accordo.Id;
        var gruppo = new DocumentSection { DocumentVersionId = versione.Id, Title = "Napoli", Order = 1, Depth = 0, SectionKey = "appgroup" };
        _db.DocumentSections.Add(gruppo);
        await _db.SaveChangesAsync();
        var meta = new ContentBlock
        {
            DocumentVersionId = versione.Id, SectionId = gruppo.Id, Order = 0, Format = BlockFormat.Table,
            BodyJson = $$"""{"Key":"grp:1","Kind":"AppGroup","MemberCallsigns":["{{Vecchio}}"]}""",
        };
        var citato = new ContentBlock
        {
            DocumentVersionId = versione.Id, SectionId = gruppo.Id, Order = 1, Format = BlockFormat.Prose, Body = "x",
            ScopeSectorId = _vecchio.Id,
        };
        _db.ContentBlocks.AddRange(meta, citato);
        await _db.SaveChangesAsync();
        _gruppoId = meta.Id;
        _bloccoId = citato.Id;
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private Task<SostituzioneEsito> SostituisciAsync() =>
        new EfCallsignRenameService(_db).SostituisciAsync(_vecchio.Id, _nuovo.Id, actorUserId: 7);

    [Fact]
    public async Task Tutto_quello_che_puntava_al_vecchio_passa_al_nuovo()
    {
        var esito = await SostituisciAsync();
        Assert.Equal((Vecchio, Nuovo, 1, 1, 1), (esito.Vecchio, esito.Nuovo, esito.Accordi, esito.Blocchi, esito.Figli));

        // Per numero: l'accordo (in forma canonica, versi coerenti), il blocco, la torre figlia, il posto in gerarchia.
        var accordo = await _db.CoordinationAgreements.AsNoTracking().Include(a => a.Sections).SingleAsync(a => a.Id == _accordoId);
        Assert.Equal((Math.Min(_nuovo.Id, _ctr.Id), Math.Max(_nuovo.Id, _ctr.Id)), (accordo.SideASectorId, accordo.SideBSectorId));
        var verso = accordo.SideASectorId == _nuovo.Id ? AgreementDirection.AtoB : AgreementDirection.BtoA;
        Assert.Equal(verso, accordo.Sections.Single().Direction);   // «dal settore APP verso il CTR», come prima
        Assert.Equal(_nuovo.Id, (await _db.ContentBlocks.AsNoTracking().SingleAsync(b => b.Id == _bloccoId)).ScopeSectorId);
        Assert.Equal(_nuovo.Id, (await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _torre.Id)).ParentSectorId);
        Assert.Equal(_ctr.Id, (await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _nuovo.Id)).ParentSectorId);

        // Per nome: il gruppo APP della vIPI di Roma, il padre nel catalogo, e l'alias per lo storico.
        var meta = (await _db.ContentBlocks.AsNoTracking().SingleAsync(b => b.Id == _gruppoId)).BodyJson!;
        Assert.Contains(Nuovo, meta);
        Assert.DoesNotContain(Vecchio, meta);
        Assert.Equal("LIRR_CTR", (await _db.AccSectors.AsNoTracking().SingleAsync(c => c.ComposePosition == Nuovo)).ParentCallsign);
        var alias = await _db.CallsignAliases.AsNoTracking().SingleAsync(a => a.OldCallsign == Vecchio);
        Assert.Equal((Nuovo, _nuovo.Id), (alias.NewCallsign, alias.SectorId));

        // Il vecchio resta un orfano senza più niente addosso: si elimina come gli altri.
        Assert.False(await _db.CoordinationAgreements.AnyAsync(a => a.SideASectorId == _vecchio.Id || a.SideBSectorId == _vecchio.Id));
        Assert.False(await _db.Sectors.AnyAsync(s => s.ParentSectorId == _vecchio.Id));
    }

    [Fact]
    public async Task La_posizione_di_un_ente_segue_e_il_documento_del_vecchio_passa_al_nuovo()
    {
        var doc = new Document { Type = DocumentType.Vipi, Title = "vIPI Napoli", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        var ente = new AtcUnit { Code = Vecchio, Name = "Napoli Approach", AccId = _lirr.Id, Mode = AtcUnitMode.InAccVipi };
        ente.Positions.Add(new AtcUnitPosition { Callsign = Vecchio, Order = 0 });
        _db.AtcUnits.Add(ente);
        _vecchio.DocumentId = doc.Id;
        await _db.SaveChangesAsync();

        Assert.True((await SostituisciAsync()).Documento);

        Assert.Equal(new[] { Nuovo }, (await new EfAtcUnitRepository(_db).FindAsync(Vecchio))!.Positions);   // il codice resta
        Assert.Equal(doc.Id, (await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _nuovo.Id)).DocumentId);
        Assert.Null((await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _vecchio.Id)).DocumentId);
    }

    [Fact]
    public async Task Un_accordo_che_il_nuovo_ha_gia_ferma_tutto_prima_di_scrivere()
    {
        _db.CoordinationAgreements.Add(new CoordinationAgreement
        {
            OwnerAccId = _lirr.Id, SideASectorId = Math.Min(_nuovo.Id, _ctr.Id), SideBSectorId = Math.Max(_nuovo.Id, _ctr.Id),
        });
        await _db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(SostituisciAsync);
        Assert.Contains("LIRR_CTR", ex.Message);
        Assert.Equal(_vecchio.Id, (await _db.Sectors.AsNoTracking().SingleAsync(s => s.Id == _torre.Id)).ParentSectorId);
    }

    [Fact]
    public async Task Non_si_sostituisce_con_un_settore_spento()
    {
        _nuovo.IsActive = false;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(SostituisciAsync);
    }
}
