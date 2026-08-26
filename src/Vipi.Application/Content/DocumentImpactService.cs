using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Documento impattato da un evento a monte (id + titolo leggibile).</summary>
public sealed record AffectedDoc(int Id, string Title);

/// <summary>Da quale parte del mondo arriva un impatto. Serve a sapere quali sezioni ne risentono.</summary>
public enum ImpactFamily
{
    /// <summary>Settori: AoR, frequenze, coordinamenti, gruppi APP.</summary>
    Sector,

    /// <summary>Aree regolamentate: la sezione «regulated».</summary>
    Area,

    /// <summary>Il documento nel suo insieme (deriva, bersaglio di release): nessuna sezione in particolare.</summary>
    Document,
}

/// <summary>Richiesta di apertura di un impatto. La frase è una <b>chiave</b> più argomenti: la compone la UI.</summary>
public sealed record RaiseImpactInput(
    int DocumentId, ImpactKind Kind, string SourceKey, string ReasonKey,
    IReadOnlyList<string>? ReasonArgs = null, bool IsPublicNow = false);

/// <summary>Una riga aperta della casella, come la leggono banner ed elenchi.</summary>
public sealed record DocumentImpactRow(
    int Id, int DocumentId, string DocumentTitle, ImpactKind Kind, string SourceKey,
    string ReasonKey, IReadOnlyList<string> ReasonArgs, bool IsPublicNow, DateTime RaisedUtc)
{
    /// <summary>Chiudibile a mano? No per i calcolati: il giro che li produce li riaprirebbe.</summary>
    public bool CanClear => !Kind.IsCalcolato();
}

/// <summary>Riassunto per un documento: quante righe aperte e di che natura (per le pill degli elenchi).</summary>
public sealed record ImpactBadge(int Total, int DaRipubblicare, int Rotti, int GiaInPubblico);

/// <summary>
/// La <b>casella degli impatti</b>: che cosa, a monte, ha toccato un documento — e quindi che cosa va riletto
/// o ripubblicato. Carta <c>docs/feature/2026-08-25-documenti-da-rivedere.md</c>.
///
/// <para>⚠️ <b>Aprire un impatto non richiede autorizzazione</b>, e non è una svista: gli eventi arrivano dai
/// giri di import, che girano <b>senza utente</b>. Fino al 25 agosto 2026 la segnalazione passava da
/// <c>IEditorTaskService.ListAllAsync()</c>, che chiama <c>EnsureAdmin()</c>: chiamata da un import sarebbe
/// esplosa con <c>EditNotAllowedException</c>, ed è il motivo per cui esisteva un solo trigger, quello
/// manuale. Il permesso serve per <b>chiudere</b> una riga, che è un atto editoriale.</para>
/// </summary>
public interface IDocumentImpactService
{
    /// <summary>Apre l'impatto su tutti i documenti legati al callsign. Ritorna quante righe ha aperto.</summary>
    Task<int> RaiseForSectorAsync(ImpactKind kind, string composePosition, string accCode,
        CancellationToken ct = default);

    /// <summary>
    /// Come <see cref="RaiseForSectorAsync"/>, ma <b>senza aprire</b>: prepara le righe che il chiamante
    /// passerà a <see cref="ReconcileAsync"/>. Serve ai rivelatori calcolati, che devono poter anche
    /// <b>chiudere</b> quel che non trovano più — e per farlo hanno bisogno dell'insieme completo, non di
    /// una riga per volta.
    /// </summary>
    Task<IReadOnlyList<RaiseImpactInput>> PrepareForSectorAsync(ImpactKind kind, string composePosition,
        string accCode, IReadOnlyList<string> args, CancellationToken ct = default);

    /// <summary>Apre l'impatto su tutti i documenti che citano l'area regolamentata.</summary>
    Task<int> RaiseForAreaAsync(ImpactKind kind, string ivaoId, string areaName, CancellationToken ct = default);

    /// <summary>
    /// Apre l'impatto su documenti <b>già noti</b>, senza passare dal reverse-lookup.
    ///
    /// <para>⚠️ Serve a chi ha appena <b>eliminato</b> ciò che collegava documento e settore: un istante
    /// dopo il <c>DELETE</c> nessuna ricerca all'indietro troverebbe più quel legame, e la segnalazione non
    /// partirebbe proprio nel caso in cui serve di più. Gli Id li porta il piano di eliminazione, che li ha
    /// calcolati quando il legame c'era ancora.</para>
    /// </summary>
    Task<int> RaiseForDocumentsAsync(ImpactKind kind, IReadOnlyCollection<int> documentIds, string sourceKey,
        IReadOnlyList<string> args, CancellationToken ct = default);

    /// <summary>Chiude le righe aperte dei tipi dati con quella origine, perché la causa non c'è più.
    /// Nessuna autorizzazione: non è un atto editoriale, è il calcolo che si accorge di essere superato.</summary>
    Task<int> ClearBySourceAsync(IReadOnlyCollection<ImpactKind> kinds, string sourceKey, CancellationToken ct = default);

    /// <summary>Le righe aperte di un documento (per il banner dell'editor).</summary>
    Task<IReadOnlyList<DocumentImpactRow>> ListOpenAsync(int documentId, CancellationToken ct = default);

    /// <summary>Quante righe aperte di un tipo, in tutto l'archivio (per la diagnostica).</summary>
    Task<int> ListOpenByKindCountAsync(ImpactKind kind, CancellationToken ct = default);

    /// <summary>Riassunto per documento, per le pill degli elenchi.</summary>
    Task<IReadOnlyDictionary<int, ImpactBadge>> CountOpenAsync(IReadOnlyCollection<int> documentIds,
        CancellationToken ct = default);

    /// <summary>Chiude una riga. Richiede il permesso di editare l'ACC del documento; rifiuta i calcolati.</summary>
    Task ClearAsync(int impactId, CancellationToken ct = default);

    /// <summary>
    /// Allinea le righe aperte di un tipo <b>calcolato</b> allo stato corrente: apre quelle nuove, chiude
    /// quelle la cui causa è sparita. È la regola «chi calcola, riconcilia».
    /// </summary>
    Task<(int Aperti, int Chiusi)> ReconcileAsync(ImpactKind kind, IReadOnlyCollection<RaiseImpactInput> attuali,
        CancellationToken ct = default);

    /// <summary>Pota le righe chiuse prima della soglia.</summary>
    Task<int> PruneClearedBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentImpactService"/>
public sealed class DocumentImpactService : IDocumentImpactService
{
    private readonly IDocumentImpactRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public DocumentImpactService(IDocumentImpactRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    /// <summary>Chiavi di frase. Stanno qui perché è chi apre l'impatto a sapere che cosa è successo; la UI le
    /// traduce, e i test le confrontano senza dipendere dalla lingua.</summary>
    public static class Reasons
    {
        public const string SectorGone = "Impact_SectorGone";
        public const string SectorStale = "Impact_SectorStale";
        public const string SectorHidden = "Impact_SectorHidden";
        public const string SectorDetached = "Impact_SectorDetached";
        public const string SectorReparented = "Impact_SectorReparented";
        public const string AreaGone = "Impact_AreaGone";
        public const string AreaChanged = "Impact_AreaChanged";
        public const string ReleaseDrift = "Impact_ReleaseDrift";
        public const string ReleaseKeyMoved = "Impact_ReleaseKeyMoved";
        public const string BrokenTarget = "Impact_BrokenTarget";

        public static string For(ImpactKind kind) => kind switch
        {
            ImpactKind.SectorGone => SectorGone,
            ImpactKind.SectorStale => SectorStale,
            ImpactKind.SectorHidden => SectorHidden,
            ImpactKind.SectorDetached => SectorDetached,
            ImpactKind.SectorReparented => SectorReparented,
            ImpactKind.AreaGone => AreaGone,
            ImpactKind.AreaChanged => AreaChanged,
            ImpactKind.ReleaseDrift => ReleaseDrift,
            ImpactKind.ReleaseKeyMoved => ReleaseKeyMoved,
            _ => BrokenTarget,
        };
    }

    public async Task<int> RaiseForSectorAsync(ImpactKind kind, string composePosition, string accCode,
        CancellationToken ct = default)
    {
        var cs = (composePosition ?? "").Trim();
        if (cs.Length == 0) return 0;

        var docs = await _repo.FindDocumentsForSectorAsync(cs, accCode ?? "", ct);
        if (docs.Count == 0) return 0;

        var live = await _repo.WithLiveSectionAsync(docs.Select(d => d.Id).ToList(), ImpactFamily.Sector, ct);

        var aperti = 0;
        foreach (var d in docs)
        {
            await _repo.RaiseAsync(new RaiseImpactInput(
                d.Id, kind, cs, Reasons.For(kind), new[] { cs }, live.Contains(d.Id)), ct);
            aperti++;
        }
        return aperti;
    }

    public async Task<IReadOnlyList<RaiseImpactInput>> PrepareForSectorAsync(ImpactKind kind,
        string composePosition, string accCode, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        var cs = (composePosition ?? "").Trim();
        if (cs.Length == 0) return Array.Empty<RaiseImpactInput>();

        var docs = await _repo.FindDocumentsForSectorAsync(cs, accCode ?? "", ct);
        if (docs.Count == 0) return Array.Empty<RaiseImpactInput>();

        var live = await _repo.WithLiveSectionAsync(docs.Select(d => d.Id).ToList(), ImpactFamily.Sector, ct);

        return docs.Select(d => new RaiseImpactInput(
            d.Id, kind, cs, Reasons.For(kind), args, live.Contains(d.Id))).ToList();
    }

    public async Task<int> RaiseForDocumentsAsync(ImpactKind kind, IReadOnlyCollection<int> documentIds,
        string sourceKey, IReadOnlyList<string> args, CancellationToken ct = default)
    {
        if (documentIds.Count == 0) return 0;

        var live = await _repo.WithLiveSectionAsync(documentIds, ImpactFamily.Sector, ct);
        var aperti = 0;
        foreach (var id in documentIds)
        {
            await _repo.RaiseAsync(new RaiseImpactInput(
                id, kind, sourceKey, Reasons.For(kind), args, live.Contains(id)), ct);
            aperti++;
        }
        return aperti;
    }

    public async Task<int> RaiseForAreaAsync(ImpactKind kind, string ivaoId, string areaName,
        CancellationToken ct = default)
    {
        var id = (ivaoId ?? "").Trim();
        if (id.Length == 0) return 0;

        var docs = await _repo.FindDocumentsForSpecialAreaAsync(id, ct);
        if (docs.Count == 0) return 0;

        var aperti = 0;
        foreach (var d in docs)
        {
            // ⚠️ Le aree regolamentate sono SEMPRE in pubblico: la sezione «regulated» è editoriale per il
            // catalogo, quindi nessuna cattura di release la congela (AccFrozenSectionProvider prende solo
            // aor/frequenze/coordinamenti/minime) e il viewer la proietta sui cataloghi correnti. Un'area che
            // cambia si vede subito, ripubblicazione o no.
            await _repo.RaiseAsync(new RaiseImpactInput(
                d.Id, kind, $"area:{id}", Reasons.For(kind),
                new[] { string.IsNullOrWhiteSpace(areaName) ? id : areaName }, IsPublicNow: true), ct);
            aperti++;
        }
        return aperti;
    }

    public Task<int> ClearBySourceAsync(IReadOnlyCollection<ImpactKind> kinds, string sourceKey,
        CancellationToken ct = default) =>
        kinds.Count == 0 || string.IsNullOrWhiteSpace(sourceKey)
            ? Task.FromResult(0)
            : _repo.ClearBySourceAsync(kinds, sourceKey.Trim(), byUserId: 0, DateTime.UtcNow, ct);

    public Task<IReadOnlyList<DocumentImpactRow>> ListOpenAsync(int documentId, CancellationToken ct = default) =>
        _repo.ListOpenAsync(documentId, ct);

    public async Task<int> ListOpenByKindCountAsync(ImpactKind kind, CancellationToken ct = default) =>
        (await _repo.ListOpenByKindAsync(kind, ct)).Count;

    public Task<IReadOnlyDictionary<int, ImpactBadge>> CountOpenAsync(IReadOnlyCollection<int> documentIds,
        CancellationToken ct = default) =>
        documentIds.Count == 0
            ? Task.FromResult<IReadOnlyDictionary<int, ImpactBadge>>(new Dictionary<int, ImpactBadge>())
            : _repo.CountOpenAsync(documentIds, ct);

    public async Task ClearAsync(int impactId, CancellationToken ct = default)
    {
        var riga = await _repo.GetOpenAsync(impactId, ct);
        if (riga is null) return;   // già chiusa da un altro, o inesistente: chiudere due volte non è un errore

        // ⚠️ I calcolati non si chiudono a mano: il giro che li produce li riaprirebbe alla prossima passata,
        // e l'utente si troverebbe a spuntare la stessa riga ogni notte. Si chiudono togliendo la causa.
        if (riga.Kind.IsCalcolato())
            throw new Aor.ValidationException(
                "Questa segnalazione la richiude da sé il controllo che l'ha aperta: si chiude risolvendo la causa.",
                "Impact_Err_Calcolato");

        // Il permesso si chiede SEMPRE. Dove l'ACC non si risolve serve essere admin: è il grado giusto per
        // agire su un documento che non si sa nemmeno a chi appartenga.
        var acc = await _repo.GetDocAccCodeAsync(riga.DocumentId, ct);
        if (acc is not null) await _authz.EnsureCanEditAccAsync(acc, ct);
        else _authz.EnsureAdmin();

        await _repo.ClearAsync(impactId, _authz.CurrentUserId ?? 0, DateTime.UtcNow, ct);
    }

    public async Task<(int Aperti, int Chiusi)> ReconcileAsync(ImpactKind kind,
        IReadOnlyCollection<RaiseImpactInput> attuali, CancellationToken ct = default)
    {
        var aperte = await _repo.ListOpenByKindAsync(kind, ct);
        var chiavi = attuali.Select(a => Chiave(a.DocumentId, a.SourceKey)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var chiusi = 0;
        foreach (var r in aperte)
        {
            if (chiavi.Contains(Chiave(r.DocumentId, r.SourceKey))) continue;
            await _repo.ClearAsync(r.Id, byUserId: 0, DateTime.UtcNow, ct);   // 0 = l'ha chiusa il calcolo
            chiusi++;
        }

        var aperti = 0;
        foreach (var a in attuali) { await _repo.RaiseAsync(a, ct); aperti++; }

        return (aperti, chiusi);
    }

    public Task<int> PruneClearedBeforeAsync(DateTime cutoffUtc, CancellationToken ct = default) =>
        _repo.PruneClearedBeforeAsync(cutoffUtc, ct);

    private static string Chiave(int documentId, string sourceKey) => $"{documentId}|{sourceKey}";
}
