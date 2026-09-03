using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Il giro della <b>deriva</b>: apre «da ripubblicare» dove la copia in vigore non dice più quel che direbbe
/// oggi, e — soprattutto — <b>richiude da sé</b> quando la causa sparisce. Carta §5-B.
///
/// <para>Il caso che vale più di tutti è il quarto: senza riconciliazione una casella alimentata da un
/// calcolo si riempie di righe che nessuno può togliere, e a quel punto non la guarda più nessuno.</para>
/// </summary>
public class ImpactDriftTests : IAsyncLifetime
{
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private VipiDbContext _db = default!;
    private EfDocumentImpactRepository _impatti = default!;
    private DocumentImpactService _servizio = default!;

    private int _docId;

    public async Task InitializeAsync()
    {
        await _conn.OpenAsync();
        var options = new DbContextOptionsBuilder<VipiDbContext>().UseSqlite(_conn).Options;
        _db = new VipiDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _impatti = new EfDocumentImpactRepository(_db);
        _servizio = new DocumentImpactService(_impatti, new AuthzSi());

        var doc = new Document
        {
            Type = DocumentType.Vipi, Title = "vIPI Roma ACC", Language = Language.It,
            Status = DocumentStatus.Published, LastUpdatedAiracCycle = "2608",
        };
        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        _docId = doc.Id;
    }

    public async Task DisposeAsync() { await _db.DisposeAsync(); await _conn.DisposeAsync(); }

    private ImpactDriftUseCase Giro(FakeAdmin admin, FakeReleaseService rel, FakeReleaseRepo repo, FakeTargets targets) =>
        new(admin, rel, repo, _servizio, new AiracService(), targets);

    private ManagedDoc Gestito() => new(
        ReleaseTargetType.AccVipi, "vIPI Roma ACC", "LIRR_NE_CTR", "LIRR",
        IsPublished: true, HasDraft: false, IsHidden: false,
        ReleaseTargetType.AccVipi, "LIRR|LIRR_NE_CTR", _docId);

    [Fact]
    public async Task Nessuna_Deriva_Nessuna_Riga()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo { Effettiva = Release("LIRR|LIRR_NE_CTR") };
        var rel = new FakeReleaseService();   // nessuna differenza
        var targets = new FakeTargets(_docId);

        var esito = await Giro(admin, rel, repo, targets).RunAsync();

        Assert.Equal(0, esito.Aperti);
        Assert.Empty(await _impatti.ListOpenAsync(_docId));
    }

    [Fact]
    public async Task Una_Deriva_Apre_Una_Riga_Che_Dice_Le_Sezioni()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo { Effettiva = Release("LIRR|LIRR_NE_CTR") };
        var rel = new FakeReleaseService
        {
            Righe = new[]
            {
                new ReleaseDiffRow("AoR", ReleaseChangeKind.Modified, 3, 4),
                new ReleaseDiffRow("Frequenze", ReleaseChangeKind.Added, null, 2),
            },
        };

        await Giro(admin, rel, repo, new FakeTargets(_docId)).RunAsync();

        var riga = Assert.Single(await _impatti.ListOpenAsync(_docId));
        Assert.Equal(ImpactKind.ReleaseDrift, riga.Kind);
        Assert.Equal("AoR, Frequenze", Assert.Single(riga.ReasonArgs));
        Assert.False(riga.CanClear);   // calcolata: la richiude il giro, non un ✓
    }

    /// <summary>
    /// La riga che mancava, e che è il cuore della segnalazione del 2 settembre 2026: la copia in vigore dice
    /// ancora il vero <b>adesso</b>, ma al ciclo entrante no. Prima il giro guardava solo a oggi, e siccome le
    /// derivate che dipendono dal ciclo nascondono quel che entra dopo, l'avviso arrivava <b>il giorno dopo il
    /// rollover</b> — a ciclo già in vigore, cioè sempre tardi di uno.
    /// </summary>
    [Fact]
    public async Task La_Deriva_Al_Ciclo_Entrante_Apre_Una_Riga_Da_Preparare()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo { Effettiva = Release("LIRR|LIRR_NE_CTR") };
        var rel = new FakeReleaseService
        {
            // Oggi tutto a posto; al ciclo entrante no.
            RigheEntranti = new[] { new ReleaseDiffRow("SID", ReleaseChangeKind.Modified, 18, 20) },
        };

        var esito = await Giro(admin, rel, repo, new FakeTargets(_docId)).RunAsync();

        Assert.Equal(1, esito.Aperti);
        var riga = Assert.Single(await _impatti.ListOpenAsync(_docId));
        Assert.Equal(ImpactKind.ReleaseDriftNextCycle, riga.Kind);
        Assert.Equal(new[] { "2609", "SID" }, riga.ReasonArgs);   // il ciclo PRIMA del riassunto: è la scadenza
        Assert.False(riga.CanClear);                              // calcolata: la richiude il giro, non un ✓
    }

    /// <summary>
    /// ⚠️ Le due righe non compaiono mai <b>insieme</b>: chi è già indietro oggi ha già la sua riga «da
    /// ripubblicare», e una seconda che dice «e sarà indietro anche domani» sarebbe rumore su una lista che
    /// vive di essere corta. Il giro non chiede nemmeno la seconda deriva.
    /// </summary>
    [Fact]
    public async Task Chi_E_Gia_Indietro_Oggi_Non_Prende_Anche_La_Riga_Del_Ciclo_Entrante()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo { Effettiva = Release("LIRR|LIRR_NE_CTR") };
        var rel = new FakeReleaseService
        {
            Righe = new[] { new ReleaseDiffRow("AoR", ReleaseChangeKind.Modified, 3, 4) },
            RigheEntranti = new[] { new ReleaseDiffRow("SID", ReleaseChangeKind.Modified, 18, 20) },
        };

        await Giro(admin, rel, repo, new FakeTargets(_docId)).RunAsync();

        var riga = Assert.Single(await _impatti.ListOpenAsync(_docId));
        Assert.Equal(ImpactKind.ReleaseDrift, riga.Kind);
        Assert.Equal(new string?[] { null }, rel.CicliChiesti);   // la seconda domanda non si fa nemmeno
    }

    /// <summary>
    /// La riconciliazione vale anche per la riga nuova: schedulata la release, al giro dopo la deriva al ciclo
    /// entrante è vuota e la riga si <b>richiude da sé</b>. È la regola «chi calcola, riconcilia» (§2 della
    /// carta), e senza di essa la casella si riempirebbe di segnalazioni che nessuno può togliere.
    /// </summary>
    [Fact]
    public async Task La_Riga_Del_Ciclo_Entrante_Si_Richiude_Da_Se()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo { Effettiva = Release("LIRR|LIRR_NE_CTR") };
        var rel = new FakeReleaseService
        {
            RigheEntranti = new[] { new ReleaseDiffRow("SID", ReleaseChangeKind.Modified, 18, 20) },
        };

        await Giro(admin, rel, repo, new FakeTargets(_docId)).RunAsync();
        Assert.Single(await _impatti.ListOpenAsync(_docId));

        rel.RigheEntranti = Array.Empty<ReleaseDiffRow>();       // qualcuno ha programmato la release
        var esito = await Giro(admin, rel, repo, new FakeTargets(_docId)).RunAsync();

        Assert.Equal(1, esito.Chiusi);
        Assert.Empty(await _impatti.ListOpenAsync(_docId));
    }

    [Fact]
    public async Task Un_Bersaglio_Che_Non_Risolve_Apre_BrokenTarget()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo { Effettiva = Release("LIRR|LIRR_NE_CTR") };

        // Il descrittore non risolve più a questo documento: è il caso «aeroporto cancellato».
        var esito = await Giro(admin, new FakeReleaseService(), repo, new FakeTargets(null)).RunAsync();

        Assert.Equal(1, esito.Aperti);
        Assert.Equal(ImpactKind.BrokenTarget, Assert.Single(await _impatti.ListOpenAsync(_docId)).Kind);
    }

    /// <summary>
    /// Le release ci sono, ma sotto un'altra chiave: il pubblico non le trova, e il documento pubblicato va
    /// muto (difetto C6). Quando è inequivocabile — stesso documento, chiave nuova senza release — la chiave
    /// si <b>ripunta</b>: la segnalazione non serve, perché il guasto è già riparato.
    /// </summary>
    [Fact]
    public async Task Una_Chiave_Spostata_Si_Ripunta_Da_Se()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo
        {
            Effettiva = null,                                  // sotto la chiave di oggi: niente
            Chiavi = new[] { "LIRR|LIRR_VECCHIO_CTR" },         // ma altrove sì, e risolve a questo documento
        };

        var esito = await Giro(admin, new FakeReleaseService(), repo, new FakeTargets(_docId)).RunAsync();

        Assert.Equal(("LIRR|LIRR_VECCHIO_CTR", "LIRR|LIRR_NE_CTR"), repo.Ripuntamento);
        Assert.Equal(1, esito.Ripuntate);
        Assert.Empty(await _impatti.ListOpenAsync(_docId));    // riparato, non segnalato
    }

    /// <summary>⚠️ L'altra metà: se il ripuntamento è rifiutato — la chiave nuova ha già una sua storia di
    /// pubblicazione — la scelta non è un calcolo, e la riga resta aperta per una persona.</summary>
    [Fact]
    public async Task Se_Non_Si_Puo_Ripuntare_Apre_ReleaseKeyMoved()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo
        {
            Effettiva = null,
            Chiavi = new[] { "LIRR|LIRR_VECCHIO_CTR" },
            PuoRipuntare = false,
        };

        var esito = await Giro(admin, new FakeReleaseService(), repo, new FakeTargets(_docId)).RunAsync();

        Assert.Equal(0, esito.Ripuntate);
        var riga = Assert.Single(await _impatti.ListOpenAsync(_docId));
        Assert.Equal(ImpactKind.ReleaseKeyMoved, riga.Kind);
        Assert.Equal("LIRR|LIRR_VECCHIO_CTR", riga.SourceKey);
    }

    /// <summary>⚠️ Il caso che tiene in vita la casella: la deriva sparisce (qualcuno ha ripubblicato) e la
    /// riga si richiude da sola, senza che nessuno debba spuntarla.</summary>
    [Fact]
    public async Task Quando_La_Deriva_Sparisce_La_Riga_Si_Richiude_Da_Se()
    {
        var admin = new FakeAdmin(Gestito());
        var repo = new FakeReleaseRepo { Effettiva = Release("LIRR|LIRR_NE_CTR") };
        var rel = new FakeReleaseService { Righe = new[] { new ReleaseDiffRow("AoR", ReleaseChangeKind.Modified, 3, 4) } };
        var targets = new FakeTargets(_docId);

        await Giro(admin, rel, repo, targets).RunAsync();
        Assert.Single(await _impatti.ListOpenAsync(_docId));

        rel.Righe = Array.Empty<ReleaseDiffRow>();             // ripubblicato: non c'è più deriva
        var esito = await Giro(admin, rel, repo, targets).RunAsync();

        Assert.Equal(1, esito.Chiusi);
        Assert.Empty(await _impatti.ListOpenAsync(_docId));
        Assert.Equal(0, (await _db.DocumentImpacts.AsNoTracking().SingleAsync()).ClearedByUserId);
    }

    [Fact]
    public async Task Le_Bozze_E_I_Nascosti_Non_Si_Guardano()
    {
        var bozza = Gestito() with { IsPublished = false };
        var nascosto = Gestito() with { IsHidden = true };

        var esito = await Giro(new FakeAdmin(bozza, nascosto), new FakeReleaseService(),
            new FakeReleaseRepo(), new FakeTargets(_docId)).RunAsync();

        Assert.Equal(0, esito.Esaminati);
    }

    private static DocRelease Release(string key) => new()
    {
        Id = 1, TargetType = ReleaseTargetType.AccVipi, TargetKey = key, VersionNumber = 1,
        ReleaseAiracCycle = "2608", ReleaseEffectiveUtc = DateTime.UtcNow.AddDays(-1),
        Status = ReleaseStatus.Effective, PayloadJson = "{}",
    };

    // ---- doppi di scena -------------------------------------------------------------------------------

    private sealed class FakeAdmin : IDocumentAdminRepository
    {
        private readonly ManagedDoc[] _docs;
        public FakeAdmin(params ManagedDoc[] docs) => _docs = docs;
        public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ManagedDoc>>(_docs);
        public Task<IReadOnlyDictionary<int, ManagedDoc>> DescribeAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, ManagedDoc>>(
                _docs.Where(d => d.DocumentId is not null && documentIds.Contains(d.DocumentId.Value))
                     .ToDictionary(d => d.DocumentId!.Value));
        public Task<ManagedDocRef?> FindAsync(ReleaseTargetType kind, string key, CancellationToken ct = default) =>
            Task.FromResult<ManagedDocRef?>(null);
        public Task<IReadOnlyDictionary<int, string>> GetTitlesAsync(IReadOnlyCollection<int> documentIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
        public Task<Vipi.Application.Content.DocumentLanguageState?> GetLanguageAsync(ManagedDocRef doc, CancellationToken ct = default) =>
            Task.FromResult<Vipi.Application.Content.DocumentLanguageState?>(null);
        public Task SetLanguageAsync(ManagedDocRef doc, Vipi.Domain.Language language, bool locked, int actorUserId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<string?> GetAccCodeAsync(ManagedDocRef doc, CancellationToken ct = default) =>
            Task.FromResult<string?>("LIRR");
        public Task SetHiddenAsync(ManagedDocRef doc, bool hidden, int actorUserId, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(ManagedDocRef doc, int actorUserId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeReleaseRepo : IReleaseRepository
    {
        public DocRelease? Effettiva { get; set; }
        public IReadOnlyList<string> Chiavi { get; set; } = Array.Empty<string>();

        /// <summary>Falso = la chiave nuova ha già delle release (o comunque il ripuntamento è rifiutato):
        /// è il caso in cui la decisione resta a una persona e la segnalazione deve restare aperta.</summary>
        public bool PuoRipuntare { get; set; } = true;
        public (string Da, string A)? Ripuntamento { get; private set; }

        public Task<int> RepointKeyAsync(ReleaseTargetType type, string oldKey, string newKey, CancellationToken ct = default)
        {
            if (!PuoRipuntare) return Task.FromResult(0);
            Ripuntamento = (oldKey, newKey);
            Chiavi = new[] { newKey };
            return Task.FromResult(1);
        }

        public Task<DocRelease?> GetEffectiveAsync(ReleaseTargetType type, string key, DateTime atUtc, CancellationToken ct = default) =>
            Task.FromResult(Effettiva);
        public Task<IReadOnlyList<string>> ListKeysWithReleasesAsync(ReleaseTargetType type, CancellationToken ct = default) =>
            Task.FromResult(Chiavi);

        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseInfo>>(Array.Empty<ReleaseInfo>());
        public Task PublishWorkingVersionAsync(ReleaseTargetType type, string key, int actorUserId, string airacCycle, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task<DocRelease?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<DocRelease?>(null);
        public Task<int> SaveReleaseAsync(ReleaseTargetType type, string key, string releaseCycle, DateTime effectiveUtc,
            string payloadJson, int createdByUserId, string? note, CancellationToken ct = default) => Task.FromResult(0);
        public Task<(ReleaseTargetType Type, string Key)?> CancelAsync(int releaseId, CancellationToken ct = default) =>
            Task.FromResult<(ReleaseTargetType, string)?>(null);
        public Task<string?> SnapshotWorkingAsync(ReleaseTargetType type, string key, string airacCycle, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
        public Task<string?> GetAuthAccCodeAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<string?>("LIRR");
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
            IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<(ReleaseTargetType, string), ReleaseSummary>>(
                new Dictionary<(ReleaseTargetType, string), ReleaseSummary>());
        public Task<int> PruneReleasesAsync(ReleaseTargetType type, string key, DateTime keepFromUtc, CancellationToken ct = default) =>
            Task.FromResult(0);
        public Task<int> PruneArchivedVersionsAsync(int documentId, int keep, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeReleaseService : IReleaseService
    {
        public IReadOnlyList<ReleaseDiffRow> Righe { get; set; } = Array.Empty<ReleaseDiffRow>();

        /// <summary>Che cosa risponde quando gli si chiede il CICLO ENTRANTE. Vuoto = niente da preparare.</summary>
        public IReadOnlyList<ReleaseDiffRow> RigheEntranti { get; set; } = Array.Empty<ReleaseDiffRow>();

        /// <summary>I cicli con cui il giro ha chiesto la deriva, per provare che ne chiede DUE e quali.</summary>
        public List<string?> CicliChiesti { get; } = new();

        public Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(ReleaseTargetType type, string key, string? alCiclo = null, CancellationToken ct = default)
        {
            CicliChiesti.Add(alCiclo);
            return Task.FromResult(alCiclo is null ? Righe : RigheEntranti);
        }

        public Vipi.Domain.Services.AiracCycleInfo NextCycle() =>
            new("2609", new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));

        public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ReleaseInfo>>(Array.Empty<ReleaseInfo>());
        // Le porte dell'unione: questi doppi non pubblicano niente, e senza unione sono le stesse di sopra.
        public Task<IReadOnlyList<Vipi.Application.Content.BersaglioUnito>> BersagliUnitiAsync(
            ReleaseTargetType type, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Vipi.Application.Content.BersaglioUnito>>(Array.Empty<Vipi.Application.Content.BersaglioUnito>());
        public Task PublishUnionAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default) =>
            PublishAsync(type, key, releaseCycle, note, ct);
        public Task PublishUnionNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default) =>
            PublishNowAsync(type, key, note, ct);

        public Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default) => Task.CompletedTask;
        public Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task CancelReleaseAsync(int releaseId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default) => Task.FromResult(ReleaseDiff.Empty);
        public Task<ReleasePreview?> GetPreviewAsync(int releaseId, ReleaseTargetType expectedType, string expectedKey, CancellationToken ct = default) => Task.FromResult<ReleasePreview?>(null);
        public Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default) => Task.FromResult<ReleaseLocation?>(null);
        public string CurrentCycle() => "2608";
        public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) => Array.Empty<AiracCycleInfo>();
        public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
            IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<(ReleaseTargetType, string), ReleaseSummary>>(
                new Dictionary<(ReleaseTargetType, string), ReleaseSummary>());
        public Task<int> PruneAllAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    /// <summary>Registry finto: risolve ogni chiave allo stesso documento (o a nessuno, per il caso rotto).</summary>
    private sealed class FakeTargets : IReleaseTargetRegistry, IReleaseTarget
    {
        private readonly int? _docId;
        public FakeTargets(int? docId) => _docId = docId;

        public IReleaseTarget For(ReleaseTargetType type) => this;
        public IReadOnlyList<IReleaseTarget> ByDescribeOrder => new IReleaseTarget[] { this };

        public ReleaseTargetType Type => ReleaseTargetType.AccVipi;
        public int DescribeOrder => 1;
        public Task<int?> ResolveDocumentIdAsync(string key, CancellationToken ct = default) => Task.FromResult(_docId);
        public Task<string?> AuthAccCodeAsync(string key, CancellationToken ct = default) => Task.FromResult<string?>("LIRR");
        public bool TryDescribe(Document doc, bool hasDraft, out ManagedDoc managed) { managed = default!; return false; }
    }

    private sealed class AuthzSi : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 1;
        public string? CurrentName => "test";
        public void EnsureAdmin() { }
    }
}
