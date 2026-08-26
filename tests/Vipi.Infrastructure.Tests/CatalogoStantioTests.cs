using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// La <b>rinomina</b>: <c>LIBD_CS0_APP</c> → <c>LIBD_CS1_APP</c>. Non sparisce niente — i cataloghi non
/// potano mai — quindi la proiezione non ha nulla da segnalare: il vecchio resta attivo, si porta dietro il
/// documento e continua a rivendicare la sua area, mentre chi controlla si connette col nome nuovo.
///
/// <para>L'unico segnale è il <b>timbro</b>: la riga vecchia smette di essere riscritta dagli import. Qui si
/// verifica che quel segnale si legga, che non produca falsi (righe aggiunte a mano, stato mancante) e che
/// il suggerimento di rinomina taccia quando la risposta non è una sola — perché la cifra in
/// <c>CS0</c>/<c>CS1</c> di solito vuol dire <b>sdoppiamento</b>, non rinomina.</para>
/// </summary>
public class CatalogoStantioTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentImpactRepository _impatti = default!;
    private EfOrphanSectorRepository _orfani = default!;
    private EfImportStateStore _stati = default!;

    private static readonly DateTime Adesso = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
    private int _docApp;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _impatti = new EfDocumentImpactRepository(_db);
        _orfani = new EfOrphanSectorRepository(_db, _impatti);
        _stati = new EfImportStateStore(_db);

        var lirr = new Acc { Code = "LIRR", Name = "Roma", CountryPrefix = "LI" };
        _db.Accs.Add(lirr);
        var libd = new Airport { Icao = "LIBD", Name = "Bari", Acc = lirr };
        _db.Airports.Add(libd);

        // Il catalogo: la posizione VECCHIA (timbro fermo a dieci giorni fa) e la NUOVA (timbro di oggi).
        _db.AirportSectors.AddRange(
            new AirportSector
            {
                ComposePosition = "LIBD_CS0_APP", AirportIcao = "LIBD", AccCode = "LIRR", Position = "APP",
                MiddleIdentifier = "CS0", ImportedAtUtc = Adesso.AddDays(-10),
            },
            new AirportSector
            {
                ComposePosition = "LIBD_CS1_APP", AirportIcao = "LIBD", AccCode = "LIRR", Position = "APP",
                MiddleIdentifier = "CS1", ImportedAtUtc = Adesso,
            },
            new AirportSector
            {
                ComposePosition = "LIBD_TWR", AirportIcao = "LIBD", AccCode = "LIRR", Position = "TWR",
                ImportedAtUtc = Adesso,
            });
        await _db.SaveChangesAsync();

        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "APP Bari", Language = Language.It, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _docApp = doc.Id;

        // Il documento sta sul nome VECCHIO: è tutto il problema.
        _db.Sectors.AddRange(
            new Sector
            {
                Acc = lirr, Callsign = "LIBD_CS0_APP", Name = "Bari APP", Type = SectorType.App,
                Kind = SectorKind.Airport, AirportIcao = "LIBD", IsProjected = true, IsActive = true,
                DocumentId = doc.Id, IsPrimary = true,
            },
            new Sector
            {
                Acc = lirr, Callsign = "LIBD_CS1_APP", Name = "Bari APP", Type = SectorType.App,
                Kind = SectorKind.Airport, AirportIcao = "LIBD", IsProjected = true, IsActive = true,
            });
        await _db.SaveChangesAsync();

        // Gli import hanno girato oggi: è il metro contro cui il timbro vecchio è vecchio.
        await _stati.MarkSuccessAsync(ImportCategories.AirportSector, Adesso);
        await _stati.MarkSuccessAsync(ImportCategories.Acc, Adesso);
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private OrphanSectorService Servizio() => new(_orfani, new AuthzSi(), _stati);

    [Fact]
    public async Task Il_Callsign_Che_La_Sorgente_Non_Manda_Piu_Compare_Fra_Gli_Orfani()
    {
        var righe = await Servizio().ListAsync();

        var riga = Assert.Single(righe);
        Assert.Equal("LIBD_CS0_APP", riga.Callsign);
        Assert.Equal(OrphanReason.NotListed, riga.Reason);
        Assert.Equal(_docApp, riga.DocumentId);          // ⚠️ il documento è rimasto sul nome vecchio
        Assert.Equal(Adesso.AddDays(-10), riga.LastSeenUtc);
    }

    [Fact]
    public async Task E_Propone_Il_Nome_Nuovo_Quando_Ce_N_E_Uno_Solo()
    {
        var riga = Assert.Single(await Servizio().ListAsync());
        Assert.Equal("LIBD_CS1_APP", riga.RenameCandidate);
    }

    /// <summary>⚠️ Due candidati non sono una rinomina, sono uno sdoppiamento: la cifra in CS0/CS1 vuol dire
    /// proprio quello. Indovinare vorrebbe dire spostare un documento sul settore sbagliato.</summary>
    [Fact]
    public async Task Con_Due_Candidati_Non_Propone_Niente()
    {
        _db.AirportSectors.Add(new AirportSector
        {
            ComposePosition = "LIBD_CS2_APP", AirportIcao = "LIBD", AccCode = "LIRR", Position = "APP",
            MiddleIdentifier = "CS2", ImportedAtUtc = Adesso,
        });
        await _db.SaveChangesAsync();

        var riga = Assert.Single(await Servizio().ListAsync());
        Assert.Null(riga.RenameCandidate);
    }

    /// <summary>⚠️ Le righe aggiunte a mano la sorgente non le ha mai mandate: il loro timbro è vecchio per
    /// costruzione, e senza il flag l'elenco nascerebbe pieno di falsi.</summary>
    [Fact]
    public async Task Le_Righe_Aggiunte_A_Mano_Non_Contano()
    {
        _db.AccSectors.Add(new AccSector
        {
            ComposePosition = "LGKR_APP", CenterId = "LIRR", Position = "APP",
            ImportedAtUtc = Adesso.AddDays(-30), IsManual = true,
        });
        await _db.SaveChangesAsync();

        var righe = await Servizio().ListAsync();
        Assert.DoesNotContain(righe, r => r.Callsign == "LGKR_APP");
    }

    /// <summary>
    /// ⚠️ La guardia di massa, e l'ha imposta la prova sui dati veri: basta un giro che <b>riesce</b> ma
    /// torna vuoto per un ente — succede, e zero elementi non è un errore — perché tutte le sue righe
    /// restino senza timbro nuovo. Il giorno dopo sarebbero trenta segnalazioni in blocco: un elenco che
    /// nessuno legge, e che soprattutto non è vero.
    /// </summary>
    [Fact]
    public async Task Se_Sono_Troppi_Non_Se_Ne_Segnala_Nessuno()
    {
        // Sei righe su otto stantìe: oltre un quarto, e sopra il minimo.
        for (var i = 1; i <= 5; i++)
            _db.AirportSectors.Add(new AirportSector
            {
                ComposePosition = $"LIBD_X{i}_APP", AirportIcao = "LIBD", AccCode = "LIRR", Position = "APP",
                ImportedAtUtc = Adesso.AddDays(-10),
            });
        await _db.SaveChangesAsync();

        Assert.Empty(await Servizio().ListAsync());
        Assert.Equal(0, (await Giro().RunAsync()).Stantii);
    }

    /// <summary>Senza l'ultimo giro riuscito non si sa niente, e «non lo sappiamo» non è «sono spariti
    /// tutti»: la stessa regola della guardia dell'avvio a freddo.</summary>
    [Fact]
    public async Task Senza_Lo_Stato_Degli_Import_Non_Si_Segnala_Niente()
    {
        _db.ImportStates.RemoveRange(await _db.ImportStates.ToListAsync());
        await _db.SaveChangesAsync();

        Assert.Empty(await Servizio().ListAsync());
    }

    [Fact]
    public async Task Un_Timbro_Fresco_Non_E_Stantio()
    {
        var vecchia = await _db.AirportSectors.FirstAsync(x => x.ComposePosition == "LIBD_CS0_APP");
        vecchia.ImportedAtUtc = Adesso;
        await _db.SaveChangesAsync();

        Assert.Empty(await Servizio().ListAsync());
    }

    /// <summary>Il giro notturno apre la segnalazione sui documenti che raccontano quel settore, e la
    /// <b>richiude da sé</b> quando la sorgente ricomincia a mandarlo.</summary>
    [Fact]
    public async Task Il_Giro_Apre_SectorStale_E_Poi_Lo_Richiude()
    {
        var giro = Giro();

        var esito = await giro.RunAsync();
        Assert.Equal(1, esito.Stantii);
        var riga = Assert.Single(await _impatti.ListOpenAsync(_docApp));
        Assert.Equal(ImpactKind.SectorStale, riga.Kind);
        Assert.Equal("LIBD_CS0_APP", riga.SourceKey);
        // ⚠️ I giorni si contano contro l'OROLOGIO VERO (`ImpactDriftUseCase` usa `DateTime.UtcNow`), mentre
        // il timbro del fixture è fisso: scritto «10» il 25 agosto, il 26 diventava «11» e il test rosso da
        // solo, senza che nessuno avesse toccato niente. Si calcola con la stessa formula del codice —
        // l'affermazione resta «tanti giorni quanti ne sono passati», che è ciò che si voleva provare.
        var attesi = Math.Max(1, (int)(DateTime.UtcNow - Adesso.AddDays(-10)).TotalDays);
        Assert.Equal(attesi.ToString(), riga.ReasonArgs[1]);          // giorni di silenzio
        Assert.False(riga.CanClear);                      // calcolata: la richiude il giro

        var vecchia = await _db.AirportSectors.FirstAsync(x => x.ComposePosition == "LIBD_CS0_APP");
        vecchia.ImportedAtUtc = Adesso.AddDays(1);
        await _db.SaveChangesAsync();

        var esito2 = await Giro().RunAsync();
        Assert.Equal(1, esito2.Chiusi);
        Assert.Empty(await _impatti.ListOpenAsync(_docApp));
    }

    /// <summary>Il giro completo, ma col servizio release finto: la deriva delle pubblicazioni non c'entra
    /// con questa storia, e montarne il motore vero qui vorrebbe dire otto dipendenze per non usarne una.</summary>
    private ImpactDriftUseCase Giro()
    {
        var registro = new ReleaseTargetRegistry(new IReleaseTarget[]
        {
            new Vipi.Infrastructure.Persistence.ReleaseTargets.VloaReleaseTarget(_db),
            new Vipi.Infrastructure.Persistence.ReleaseTargets.AccVipiReleaseTarget(_db),
            new Vipi.Infrastructure.Persistence.ReleaseTargets.AppReleaseTarget(_db),
            new Vipi.Infrastructure.Persistence.ReleaseTargets.AirportReleaseTarget(_db),
        });
        var releases = new EfReleaseRepository(_db, registro);
        return new ImpactDriftUseCase(
            new EfDocumentAdminRepository(_db, registro, releases),
            new ReleaseFinto(),
            releases,
            new DocumentImpactService(_impatti, new AuthzSi()),
            new Vipi.Domain.Services.AiracService(),
            registro,
            _orfani,
            _stati);
    }

    private sealed class ReleaseFinto : IReleaseService
    {
        public Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseDiffRow>>(Array.Empty<ReleaseDiffRow>());
        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseInfo>>(Array.Empty<ReleaseInfo>());
        public Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default) => Task.CompletedTask;
        public Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task CancelReleaseAsync(int releaseId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default) => Task.FromResult(ReleaseDiff.Empty);
        public Task<ReleasePreview?> GetPreviewAsync(int releaseId, CancellationToken ct = default) => Task.FromResult<ReleasePreview?>(null);
        public Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default) => Task.FromResult<ReleaseLocation?>(null);
        public string CurrentCycle() => "2608";
        public IReadOnlyList<Vipi.Domain.Services.AiracCycleInfo> UpcomingCycles(int count) => Array.Empty<Vipi.Domain.Services.AiracCycleInfo>();
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
            IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<(ReleaseTargetType, string), ReleaseSummary>>(
                new Dictionary<(ReleaseTargetType, string), ReleaseSummary>());
        public Task<int> PruneAllAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class AuthzSi : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public Task EnsureCanEditAccAsync(string accCode, CancellationToken ct = default) => Task.CompletedTask;
        public Task EnsureCanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> CanEditAccAsync(string accCode, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditDocumentAsync(int documentId, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> CanEditAnythingAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlyList<GrantRow>> ListGrantsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<GrantRow>>(Array.Empty<GrantRow>());
        public Task<int> AddGrantAsync(int UserId, string? displayName, string accCode, CancellationToken ct = default) => Task.FromResult(0);
        public Task RevokeGrantAsync(int grantId, CancellationToken ct = default) => Task.CompletedTask;
        public void EnsureAdmin() { }
    }
}
