using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il giro di «chiedi alla sorgente»: chi viene interrogato, con quale indirizzo, e <b>quando</b>.
///
/// <para>Le politiche stanno in <c>DeletionRules</c> (provate senza database in <c>DeletionRulesTests</c>) e
/// il filo in <c>SourcePresenceProbeTests</c>. Qui si prova la terza cosa, che non sta né di qua né di là:
/// che il verdetto mostrato nella finestra <b>non</b> sia quello che autorizza il <c>DELETE</c>, e che la
/// domanda si rifaccia al momento di cancellare.</para>
///
/// <para>Carta: <c>docs/feature/2026-08-26-chiedere-alla-sorgente.md</c>.</para>
/// </summary>
public class DeletionProbeTests : IDisposable
{

    // ⚠️ Questi test leggono i MESSAGGI, e i messaggi hanno due lingue (Messaggio.Lingua): senza fissare la
    // cultura passerebbero in Italia e cadrebbero su una macchina inglese. Si fissa qui, una volta per la
    // classe, invece che in ogni test.
    private readonly CulturaDiProva _lingua = CulturaDiProva.Italiana();
    public DeletionProbeTests() { }

    public void Dispose() => _lingua.Dispose();
    private static readonly DateTime Adesso = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Penultimo = Adesso.AddDays(-1);

    // ── A chi si chiede, e con quale indirizzo ───────────────────────────────────────────────────────

    [Fact]
    public async Task Un_settore_di_ACC_si_chiede_all_ente()
    {
        var sorgente = new SorgenteFinta(SourceProbeResult.Assente("non c'è"));
        var s = Costruisci(sorgente, settore: Settore());

        await s.VerificaAllaSorgenteAsync(DeletionTarget.Sector(1));

        var chiesto = Assert.Single(sorgente.Chiesti);
        Assert.Equal(SourceProbeKind.AccSector, chiesto.Kind);
        Assert.Equal("LIRR_W_CTR", chiesto.Key);
        Assert.Equal("LIRR", chiesto.Owner);
    }

    [Fact]
    public async Task Una_postazione_di_scalo_si_chiede_all_aeroporto()
    {
        // ⚠️ Due indirizzari diversi nella sorgente: i subcenter stanno sotto l'ACC, le postazioni sotto
        // l'ICAO. Con l'indirizzo sbagliato la controprova cadrebbe sull'elenco di un altro, e la risposta
        // «non ti nomina» sarebbe vera e inutile.
        var sorgente = new SorgenteFinta(SourceProbeResult.Assente("non c'è"));
        var s = Costruisci(sorgente, settore: Settore("LIRF_GND", SectorKind.Airport, icao: "LIRF"));

        await s.VerificaAllaSorgenteAsync(DeletionTarget.Sector(1));

        var chiesto = Assert.Single(sorgente.Chiesti);
        Assert.Equal(SourceProbeKind.AirportSector, chiesto.Kind);
        Assert.Equal("LIRF", chiesto.Owner);
    }

    [Fact]
    public async Task A_una_riga_aggiunta_a_mano_non_si_chiede_niente()
    {
        // La sorgente non l'ha mai mandata: chiederle se c'è ancora è una domanda senza senso, e la
        // risposta «non ce l'ho» non proverebbe nulla. D8 già non la tocca.
        var sorgente = new SorgenteFinta(SourceProbeResult.Assente("non c'è"));
        var s = Costruisci(sorgente, settore: Settore() with { CatalogoManuale = true });

        var esito = await s.VerificaAllaSorgenteAsync(DeletionTarget.Sector(1));

        Assert.Empty(sorgente.Chiesti);
        Assert.Equal(SourcePresence.NonSiSa, esito.Prova.Esito);
        Assert.Contains("a mano", esito.Prova.Motivo);
    }

    [Fact]
    public async Task A_un_documento_non_si_chiede_niente()
    {
        var sorgente = new SorgenteFinta(SourceProbeResult.Assente("non c'è"));
        var s = Costruisci(sorgente, documento: new DocumentFacts(9, "vIPI Roma", DocumentType.Vipi, false, 0,
            Array.Empty<string>(), null));

        var esito = await s.VerificaAllaSorgenteAsync(DeletionTarget.Document(9));

        Assert.Empty(sorgente.Chiesti);
        Assert.Equal(SourcePresence.NonSiSa, esito.Prova.Esito);
    }

    // ── Che cosa fa il verdetto ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Il_verdetto_assente_scioglie_il_blocco_nel_piano_che_torna()
    {
        var sorgente = new SorgenteFinta(SourceProbeResult.Assente("LIRR ne elenca 7 e questo non c'è"));
        var s = Costruisci(sorgente, settore: Settore(timbro: Adesso.AddHours(-1)));

        Assert.False((await s.AnteprimaAsync(DeletionTarget.Sector(1))).Eliminabile);

        var esito = await s.VerificaAllaSorgenteAsync(DeletionTarget.Sector(1));
        Assert.True(esito.Piano.Eliminabile);
        Assert.Empty(esito.Piano.Blocca);
    }

    [Fact]
    public async Task Il_verdetto_presente_non_scioglie_niente()
    {
        var sorgente = new SorgenteFinta(SourceProbeResult.Presente("la sorgente lo manda"));
        var s = Costruisci(sorgente, settore: Settore(timbro: Adesso.AddHours(-1)));

        var esito = await s.VerificaAllaSorgenteAsync(DeletionTarget.Sector(1));

        Assert.False(esito.Piano.Eliminabile);
        Assert.True(esito.Piano.LaSorgenteTrattiene);
    }

    [Fact]
    public async Task Il_verdetto_non_si_sa_lascia_l_attesa_dov_era()
    {
        var sorgente = new SorgenteFinta(SourceProbeResult.NonSiSa("502"));
        var s = Costruisci(sorgente, settore: Settore(timbro: Adesso.AddHours(-1)));

        Assert.False((await s.VerificaAllaSorgenteAsync(DeletionTarget.Sector(1))).Piano.Eliminabile);
    }

    // ── Il momento che conta ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task La_domanda_si_rifa_al_momento_di_cancellare()
    {
        // ⚠️ Il verdetto della finestra ha autorizzato il TASTO, non il DELETE: fra i due c'è il tempo che
        // l'utente impiega a leggere, e in quel tempo un import può aver rimesso in archivio ciò che la
        // sorgente aveva appena smesso di mandare.
        var sorgente = new SorgenteFinta(SourceProbeResult.Assente("non c'è"));
        var repo = new RepoFinto { Settore = Settore(timbro: Adesso.AddHours(-1)) };
        var s = Costruisci(sorgente, repo: repo);

        await s.VerificaAllaSorgenteAsync(DeletionTarget.Sector(1));
        Assert.Single(sorgente.Chiesti);

        await s.EliminaAsync(DeletionTarget.Sector(1), conVerificaAllaSorgente: true);
        Assert.Equal(2, sorgente.Chiesti.Count);
        Assert.NotNull(repo.Applicato);
    }

    [Fact]
    public async Task Senza_l_ordine_di_chiedere_non_si_chiede_e_il_blocco_regge()
    {
        // Il parametro è un ORDINE di chiedere, non un verdetto: chi chiama non può passare una risposta
        // già presa. Senza, l'eliminazione resta quella di prima — e D8 la ferma.
        var sorgente = new SorgenteFinta(SourceProbeResult.Assente("non c'è"));
        var repo = new RepoFinto { Settore = Settore(timbro: Adesso.AddHours(-1)) };
        var s = Costruisci(sorgente, repo: repo);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => s.EliminaAsync(DeletionTarget.Sector(1)));

        Assert.Empty(sorgente.Chiesti);
        Assert.Contains("la manda ancora", ex.Message);
        Assert.Null(repo.Applicato);
    }

    [Fact]
    public async Task Se_al_momento_del_DELETE_la_sorgente_cambia_idea_non_si_cancella()
    {
        // La finestra aveva un «assente»; alla conferma la sorgente risponde che ce l'ha ancora. Vince
        // l'ultima risposta, che è quella dell'istante in cui si sta per cancellare davvero.
        var sorgente = new SorgenteFinta(
            SourceProbeResult.Assente("non c'è"),
            SourceProbeResult.Presente("eccolo, è tornato"));
        var repo = new RepoFinto { Settore = Settore(timbro: Adesso.AddHours(-1)) };
        var s = Costruisci(sorgente, repo: repo);

        Assert.True((await s.VerificaAllaSorgenteAsync(DeletionTarget.Sector(1))).Piano.Eliminabile);

        await Assert.ThrowsAsync<ValidationException>(
            () => s.EliminaAsync(DeletionTarget.Sector(1), conVerificaAllaSorgente: true));
        Assert.Null(repo.Applicato);
    }

    [Fact]
    public async Task Le_tracce_della_prova_finiscono_nell_audit()
    {
        // Senza, il registro mostrerebbe una cancellazione che le protezioni vietavano, e nessun modo di
        // sapere perché è passata.
        var sorgente = new SorgenteFinta(
            SourceProbeResult.Assente("non c'è", "GET /v2/subcenters/LIRR_W_CTR → 404; GET /v2/centers/LIRR/subcenters → 200, 7 elementi"));
        var repo = new RepoFinto { Settore = Settore(timbro: Adesso.AddHours(-1)) };
        var s = Costruisci(sorgente, repo: repo);

        await s.EliminaAsync(DeletionTarget.Sector(1), conVerificaAllaSorgente: true);

        Assert.Contains("404", repo.ProvaScritta);
        Assert.Contains("7 elementi", repo.ProvaScritta);
    }

    [Fact]
    public async Task Un_eliminazione_ordinaria_non_scrive_nessuna_prova()
    {
        var repo = new RepoFinto { Settore = Settore() };   // timbro vecchio: passa da sé
        var s = Costruisci(new SorgenteFinta(SourceProbeResult.Assente("non c'è")), repo: repo);

        await s.EliminaAsync(DeletionTarget.Sector(1));

        Assert.NotNull(repo.Applicato);
        Assert.Null(repo.ProvaScritta);
    }

    // ── Impalcatura ──────────────────────────────────────────────────────────────────────────────────

    private static SectorFacts Settore(string callsign = "LIRR_W_CTR", SectorKind kind = SectorKind.Acc,
        string? icao = null, DateTime? timbro = null) =>
        new(1, callsign, "Roma Ovest", "LIRR", SectorType.Ctr, kind, icao is null ? null : 3, icao,
            null, null, true, false, timbro ?? Adesso.AddDays(-5),
            Array.Empty<ChildFacts>(), Array.Empty<CatalogChildFacts>(),
            Array.Empty<DocRefFacts>(), Array.Empty<AgreementFacts>());

    private static DeletionService Costruisci(SorgenteFinta sorgente, RepoFinto? repo = null,
        SectorFacts? settore = null, DocumentFacts? documento = null)
    {
        repo ??= new RepoFinto { Settore = settore, Documento = documento };
        return new DeletionService(repo, new AuthzFinta(), new StatiFinti(), new ImpattiFinti(),
            new DocumentiFinti(), new IncarichiFinti(), sorgente);
    }

    /// <summary>Risponde in coda: la prima risposta alla prima domanda, l'ultima si ripete.</summary>
    private sealed class SorgenteFinta : ISourcePresenceProbe
    {
        private readonly SourceProbeResult[] _risposte;
        public List<SourceProbeTarget> Chiesti { get; } = new();

        public SorgenteFinta(params SourceProbeResult[] risposte) => _risposte = risposte;

        public Task<SourceProbeResult> ChiediAsync(SourceProbeTarget bersaglio, CancellationToken ct = default)
        {
            Chiesti.Add(bersaglio);
            return Task.FromResult(_risposte[Math.Min(Chiesti.Count - 1, _risposte.Length - 1)]);
        }
    }

    private sealed class RepoFinto : IDeletionRepository
    {
        public SectorFacts? Settore { get; init; }
        public DocumentFacts? Documento { get; init; }
        public DeletionActions? Applicato { get; private set; }
        public string? ProvaScritta { get; private set; }

        public Task<SectorFacts?> SectorFactsAsync(int sectorId, CancellationToken ct = default) =>
            Task.FromResult(Settore);

        public Task<int?> SectorIdByCallsignAsync(string callsign, CancellationToken ct = default) =>
            Task.FromResult<int?>(1);

        public Task<DocumentFacts?> DocumentFactsAsync(int documentId, CancellationToken ct = default) =>
            Task.FromResult(Documento);

        public Task ApplyAsync(DeletionActions azioni, int actorUserId, string? provaSorgente = null,
            CancellationToken ct = default)
        {
            Applicato = azioni;
            ProvaScritta = provaSorgente;
            return Task.CompletedTask;
        }

        public Task<AirportFacts?> AirportFactsAsync(int airportId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<AccFacts?> AccFactsAsync(string accCode, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<AffectedDoc>> AllDocumentsAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DeleteUnmanagedDocumentAsync(int documentId, int actorUserId, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task<NeighbourFacts?> NeighbourFactsAsync(int candidateId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<AreaFacts?> AreaFactsAsync(string ivaoId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> ReleaseCountAsync(ReleaseTargetType tipo, string chiave, CancellationToken ct = default) =>
            Task.FromResult(0);
    }

    private sealed class StatiFinti : IImportStateStore
    {
        public Task<DateTime?> GetPrevSuccessAsync(string category, CancellationToken ct = default) =>
            Task.FromResult<DateTime?>(Penultimo);
        public Task<DateTime?> GetLastSuccessAsync(string category, CancellationToken ct = default) =>
            Task.FromResult<DateTime?>(Adesso);
        public Task MarkSuccessAsync(string category, DateTime utc, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task MarkFailureAsync(string category, DateTime utc, string error, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<ImportState>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ImportState>>(Array.Empty<ImportState>());
    }

    private sealed class AuthzFinta : IEditAuthorizationService
    {
        public bool IsAdmin => true;
        public VipiRole Role => IsAdmin ? VipiRole.Admin : VipiRole.User;
        public int? CurrentUserId => 555;
        public string? CurrentName => "Chi Elimina";
        public void EnsureAdmin() { }
    }

    private sealed class ImpattiFinti : IDocumentImpactService
    {
        public Task<int> RaiseForDocumentsAsync(ImpactKind kind, IReadOnlyCollection<int> documentIds,
            string sourceKey, IReadOnlyList<string> args, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> RaiseForAreaAsync(ImpactKind kind, string ivaoId, string areaName, CancellationToken ct = default) =>
            Task.FromResult(0);
        public Task<int> RaiseForSectorAsync(ImpactKind kind, string composePosition, string accCode,
            CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<RaiseImpactInput>> PrepareForSectorAsync(ImpactKind kind, string composePosition,
            string accCode, IReadOnlyList<string> args, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> ClearBySourceAsync(IReadOnlyCollection<ImpactKind> kinds, string sourceKey,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DocumentImpactRow>> ListOpenAsync(int documentId, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> ListOpenByKindCountAsync(ImpactKind kind, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyDictionary<int, ImpactBadge>> CountOpenAsync(IReadOnlyCollection<int> documentIds,
            CancellationToken ct = default) => throw new NotSupportedException();
        public Task ClearAsync(int impactId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<(int Aperti, int Chiusi)> ReconcileAsync(ImpactKind kind,
            IReadOnlyCollection<RaiseImpactInput> attuali, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> PruneClearedBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class DocumentiFinti : IDocumentAdminService
    {
        public Task<IReadOnlyList<ManagedDoc>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ManagedDoc>>(Array.Empty<ManagedDoc>());
        public Task<DocumentLanguageState?> GetLanguageAsync(ManagedDocRef doc, CancellationToken ct = default) =>
            Task.FromResult<DocumentLanguageState?>(null);
        public Task SetLanguageAsync(ManagedDocRef doc, Vipi.Domain.Language language, bool locked, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task SetHiddenAsync(ManagedDocRef doc, bool hidden, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(ManagedDocRef doc, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class IncarichiFinti : IEditorTaskService
    {
        public Task<IReadOnlyList<EditorTask>> ListAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EditorTask>>(Array.Empty<EditorTask>());
        public Task<IReadOnlyList<EditorTask>> ListMineAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<int> CreateAsync(EditorTaskInput input, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task UpdateStatusAsync(int id, EditorTaskStatus status, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task AssignAsync(int id, int assigneeUserId, string? assigneeName, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public string CurrentCycle() => "2609";
        public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) => Array.Empty<AiracCycleInfo>();
        public bool IsOverdue(EditorTask t) => false;
    }
}
