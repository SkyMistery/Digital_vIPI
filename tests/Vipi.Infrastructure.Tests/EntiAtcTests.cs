using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Vipi.Infrastructure.Persistence.ReleaseTargets;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Gli ENTI ATC (S49, committente, 29 settembre 2026): la vIPI APP è dell'ente, non di un nominativo IVAO.
///
/// <para>Il caso vero è Pratica di Mare: la posizione giusta è <c>LIRE_TWR</c> — una torre che fa anche
/// l'avvicinamento — ma su IVAO c'è <c>LIRE_APP</c> «per far funzionare tutto», perché la vIPI APP poteva stare
/// solo su un settore APP. E un APP che cambiava stato perdeva il documento: spuntato «remotizzato», il
/// descrittore delle pubblicazioni non lo riconosceva più.</para>
/// </summary>
public class EntiAtcTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private Acc _acc = default!;
    private int _docId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        _db = new VipiDbContext(new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options);
        await _db.Database.EnsureCreatedAsync();
        _acc = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(_acc);
        var lire = new Airport { Icao = "LIRE", Name = "Pratica di Mare", Acc = _acc };
        _db.Airports.Add(lire);
        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "Pratica Tower", Language = Language.It,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2610",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _docId = doc.Id;

        // La forma di PRIMA del 29 settembre 2026: la vIPI APP sta sul settore APP.
        _db.Sectors.Add(new Sector
        {
            Acc = _acc, Callsign = "LIRE_APP", Name = "LIRE Approach", Type = SectorType.App, Kind = SectorKind.Airport,
            ApproachKind = ApproachKind.Standalone, AirportId = lire.Id, AirportIcao = "LIRE", IsActive = true,
            DocumentId = doc.Id, IsPrimary = true,
        });
        _db.Sectors.Add(new Sector
        {
            Acc = _acc, Callsign = "LIRE_TWR", Name = "LIRE Tower", Type = SectorType.Twr, Kind = SectorKind.Airport,
            AirportId = lire.Id, AirportIcao = "LIRE", IsActive = true,
        });
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = "LIRE_APP", AirportIcao = "LIRE", AccCode = "LIRR", Position = "APP",
            AtcCallsign = "Pratica Tower", Frequency = "118.050", ImportedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private Task<int> PonteAsync() => new EfDocumentMaintenance(_db).LinkAppUnitsAsync();

    private async Task<Document> DocumentoConEnteAsync() =>
        await _db.Documents.AsNoTracking().Include(d => d.AtcUnit).ThenInclude(u => u!.Acc)
            .Include(d => d.Sectors).FirstAsync(d => d.Id == _docId);

    [Fact]
    public async Task Il_ponte_porta_la_vipi_app_sul_suo_ente_e_sgancia_il_settore()
    {
        Assert.Equal(1, await PonteAsync());

        var ente = await new EfAtcUnitRepository(_db).FindAsync("LIRE_APP");
        Assert.NotNull(ente);
        // Il codice è il nominativo che portava il documento: è la chiave sotto cui è già pubblicato.
        Assert.Equal("LIRE_APP", ente!.Code);
        Assert.Equal("Pratica Tower", ente.Name);   // il nome IVAO dal catalogo
        Assert.Equal("LIRR", ente.AccCode);
        Assert.Equal(_docId, ente.DocumentId);
        Assert.Equal(new[] { "LIRE_APP" }, ente.Positions);

        var settore = await _db.Sectors.AsNoTracking().SingleAsync(s => s.Callsign == "LIRE_APP");
        Assert.Null(settore.DocumentId);
        Assert.False(settore.IsPrimary);

        // Idempotente: gira a ogni avvio.
        Assert.Equal(0, await PonteAsync());
        Assert.Equal(1, await _db.AtcUnits.CountAsync());
    }

    [Fact]
    public async Task Il_ponte_non_tocca_la_vipi_di_uno_scalo_ne_quella_di_un_acc()
    {
        var scalo = new Document { Type = DocumentType.Vipi, Title = "vIPI LIRA", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        var acc = new Document { Type = DocumentType.Vipi, Title = "vIPI Roma", Language = Language.It, LastUpdatedAiracCycle = "2610" };
        _db.Documents.AddRange(scalo, acc);
        await _db.SaveChangesAsync();
        _db.Airports.Add(new Airport { Icao = "LIRA", Name = "Ciampino", Acc = _acc, DocumentId = scalo.Id });
        // Due legami storici che NON sono vIPI APP: un APP legato alla vIPI dello scalo, uno alla vIPI ACC.
        _db.Sectors.Add(new Sector { Acc = _acc, Callsign = "LIRA_APP", Name = "x", Type = SectorType.App, Kind = SectorKind.Airport, ApproachKind = ApproachKind.Standalone, IsActive = true, DocumentId = scalo.Id });
        _db.Sectors.Add(new Sector { Acc = _acc, Callsign = "LIRR_CTR", Name = "x", Type = SectorType.Ctr, Kind = SectorKind.Acc, IsActive = true, DocumentId = acc.Id, IsPrimary = true });
        _db.Sectors.Add(new Sector { Acc = _acc, Callsign = "LIRR_W_APP", Name = "x", Type = SectorType.App, Kind = SectorKind.Acc, ApproachKind = ApproachKind.Remotized, IsActive = true, DocumentId = acc.Id });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await PonteAsync());   // solo Pratica

        Assert.False(await _db.AtcUnits.AnyAsync(u => u.DocumentId == scalo.Id || u.DocumentId == acc.Id));
        Assert.Equal(acc.Id, (await _db.Sectors.AsNoTracking().SingleAsync(s => s.Callsign == "LIRR_W_APP")).DocumentId);
    }

    [Fact]
    public async Task Pratica_passa_su_LIRE_TWR_e_documento_e_pubblicazioni_restano()
    {
        await PonteAsync();
        var enti = new AtcUnitService(new EfAtcUnitRepository(_db), new Authz(VipiRole.Editor));
        var ente = (await enti.FindAsync("LIRE_APP"))!;

        await enti.AddPositionAsync(ente.Id, "lire_twr");
        await enti.MakePrimaryAsync(ente.Id, "LIRE_TWR");
        await enti.RemovePositionAsync(ente.Id, "LIRE_APP");

        // La torre trova l'ente, e da lei parte la derivazione.
        var perTorre = await new EfAppDerivationRepository(_db).ResolveForDocumentAsync("LIRE_TWR");
        Assert.NotNull(perTorre);
        Assert.Equal("LIRE_APP", perTorre!.Code);
        Assert.Equal("LIRE_TWR", perTorre.Seme);
        Assert.Equal(_docId, perTorre.DocumentId);

        // La chiave di pubblicazione è il codice: quella di ieri vale ancora, anche col nominativo andato via.
        var target = new AppReleaseTarget(_db);
        Assert.Equal(_docId, await target.ResolveDocumentIdAsync("LIRE_APP"));
        Assert.Equal("LIRR", await target.AuthAccCodeAsync("LIRE_TWR"));
        Assert.True(target.TryDescribe(await DocumentoConEnteAsync(), false, out var descritto));
        Assert.Equal("LIRE_APP", descritto.ReleaseKey);

        // E la sparizione di una posizione arriva alla vIPI dell'ente.
        var avvisati = await new EfDocumentImpactRepository(_db).FindDocumentsForSectorAsync("LIRE_TWR", "LIRR");
        Assert.Contains(avvisati, d => d.Id == _docId);
    }

    [Fact]
    public async Task Un_app_spuntato_remotizzato_non_perde_piu_la_sua_vipi()
    {
        // 🔴 Il guasto di prima: col descrittore che guardava il settore primario, un APP diventato «remotizzato»
        // rendeva la sua vIPI APP irraggiungibile — finiva nel descrittore d'aeroporto con l'ICAO vuoto.
        await PonteAsync();
        var settore = await _db.Sectors.SingleAsync(s => s.Callsign == "LIRE_APP");
        settore.ApproachKind = ApproachKind.Remotized;
        await _db.SaveChangesAsync();

        var doc = await DocumentoConEnteAsync();
        Assert.True(new AppReleaseTarget(_db).TryDescribe(doc, false, out var app));
        Assert.Equal("LIRE_APP", app.ReleaseKey);
        Assert.False(new AirportReleaseTarget(_db).TryDescribe(doc, false, out _));
        Assert.Equal(_docId, await new AppReleaseTarget(_db).ResolveDocumentIdAsync("LIRE_APP"));
    }

    [Fact]
    public async Task Una_posizione_appartiene_a_un_ente_solo_e_un_nominativo_deve_esserlo()
    {
        await PonteAsync();
        var repo = new EfAtcUnitRepository(_db);
        var altro = await repo.EnsureDocumentAsync("LIRF_APP", "Roma Approach", "LIRR", SectionProfile.App, 0);
        var enti = new AtcUnitService(repo, new Authz(VipiRole.Editor));
        var pratica = (await enti.FindAsync("LIRE_APP"))!;
        var roma = (await enti.FindAsync("LIRF_APP"))!;
        Assert.Equal(altro, roma.DocumentId);

        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => enti.AddPositionAsync(roma.Id, "LIRE_APP"));
        await Assert.ThrowsAsync<Vipi.Application.Aor.ValidationException>(() => enti.AddPositionAsync(pratica.Id, "pratica"));
        await Assert.ThrowsAsync<EditNotAllowedException>(
            () => new AtcUnitService(repo, new Authz(VipiRole.User)).AddPositionAsync(pratica.Id, "LIRE_TWR"));
    }

    private sealed class Authz : IEditAuthorizationService
    {
        public Authz(VipiRole livello) => Role = livello;
        public VipiRole Role { get; }
        public bool IsAdmin => Role >= VipiRole.Admin;
        public int? CurrentUserId => 704798;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
