using System.Text.Json;
using Microsoft.Extensions.Options;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>
/// Use-case delle release AIRAC: pubblica lo snapshot editoriale del documento a un ciclo di rilascio (schedulato o
/// immediato), elenca la timeline. Lo stato live resta la bozza; la release ne congela una fotografia visibile al
/// pubblico dal ciclo di rilascio in poi. Scritture gated via authz ACC.
/// </summary>
public interface IReleaseService
{
    Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>Pubblica lo snapshot corrente al ciclo AIRAC indicato (entra in vigore alla sua data efficace).</summary>
    Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default);

    /// <summary>Forza la pubblicazione immediata (review): ciclo corrente, effettiva adesso.</summary>
    Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default);

    /// <summary>Migrazione A (doc 10 §3f): per ogni documento <c>Published</c> e non nascosto SENZA release effettiva,
    /// genera una copia statica al ciclo corrente (effettiva adesso), così togliere il fallback live pubblico (S6b) non
    /// lascia buchi. Operazione di sistema (nessuna authz), idempotente: salta i bersagli già coperti e i documenti
    /// senza contenuto. Ritorna il numero di release generate.</summary>
    Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default);

    /// <summary>Annulla una release (per Id). Authz sull'ACC del bersaglio.</summary>
    Task CancelReleaseAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Riepilogo differenze di una release rispetto a quella in vigore (o allo stato pubblicato/live).</summary>
    Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Anteprima di una release: metadati + <see cref="RawDocument"/> del payload. Vale per TUTTI i tipi —
    /// dal doc 08 condividono <c>DocReleasePayload</c> — e non solo per vLOA/aeroporto come diceva questo commento.
    /// La vIPI ACC ha comunque una porta propria (<c>IAccDocumentService.LoadForReleaseAsync</c>), che oltre allo
    /// snapshot ne assembla i blocchi: è la stessa fotografia, letta con l'attrezzo del suo tipo. Authz ACC.</summary>
    Task<ReleasePreview?> GetPreviewAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Identità (tipo/chiave/ciclo/ACC) di una release, per risolvere la route del viewer tipizzato.
    /// Authz ACC come le altre operazioni di release. null se inesistente.</summary>
    Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Ciclo AIRAC corrente.</summary>
    string CurrentCycle();

    /// <summary>I prossimi <paramref name="count"/> cicli AIRAC (corrente incluso), per il selettore di rilascio.</summary>
    IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count);

    /// <summary>Riepilogo release (in vigore / prossima schedulata) per un insieme di bersagli, in un'unica query,
    /// per mostrare lo stato sulle righe collassate dell'elenco. Chiave = (TargetType, TargetKey).</summary>
    Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
        IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default);

    /// <summary>
    /// <b>Deriva</b>: che cosa direbbe oggi la copia pubblicata se la si rifacesse adesso, confrontato con
    /// quella in vigore. Vuoto = la release dice ancora il vero (o non c'è una release in vigore, e allora
    /// non c'è niente da cui derivare). Operazione di sistema: nessuna autorizzazione, la chiama un giro.
    ///
    /// <para>⚠️ Il confronto è quello del <c>Diff</c> fra release — voce (sezione/blocco) → conteggio degli
    /// elementi — quindi vede una sezione che cambia numero di righe, <b>non</b> un testo riscritto dentro
    /// una riga esistente. È un limite dichiarato: la casella deve promettere quel che misura.</para>
    /// </summary>
    Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(ReleaseTargetType type, string key, CancellationToken ct = default);

    /// <summary>Sweep di retention su tutti i documenti gestiti (system op, come <see cref="BackfillMissingReleasesAsync"/>):
    /// pota release Superseded oltre soglia e versioni Archived oltre N per ciascun bersaglio. Idempotente. Ritorna il
    /// numero di versioni archiviate rimosse.</summary>
    Task<int> PruneAllAsync(CancellationToken ct = default);
}

/// <inheritdoc cref="IReleaseService"/>
public sealed class ReleaseService : IReleaseService
{
    private readonly IReleaseRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IAiracService _airac;
    private readonly IFrozenSectionRegistry _frozen;
    private readonly IDocumentAdminRepository _admin;
    private readonly IEditingRepository _editing;
    private readonly IReleaseTargetRegistry _targets;
    private readonly ReleaseRetentionOptions _retention;
    private readonly IUnitOfWork _uow;

    public ReleaseService(IReleaseRepository repo, IEditAuthorizationService authz, IAiracService airac,
        IFrozenSectionRegistry frozen, IDocumentAdminRepository admin, IEditingRepository editing,
        IReleaseTargetRegistry targets, IOptions<ReleaseRetentionOptions> retention, IUnitOfWork uow,
        ShapeReleaseContext? shapeCycle = null)
    {
        _shapeCycle = shapeCycle;
        _repo = repo;
        _authz = authz;
        _airac = airac;
        _frozen = frozen;
        _admin = admin;
        _editing = editing;
        _targets = targets;
        _retention = retention.Value;
        _uow = uow;
    }

    /// <summary>Il contesto che dice alla lettura delle shape «sto congelando per questo ciclo». Opzionale:
    /// senza, il congelamento prende le geometrie correnti — cioè il comportamento di prima del gate.</summary>
    private readonly ShapeReleaseContext? _shapeCycle;

    public Task<IReadOnlyList<ReleaseInfo>> ListAsync(ReleaseTargetType type, string key, CancellationToken ct = default) =>
        _repo.ListAsync(type, key, ct);

    public Task<IReadOnlyDictionary<(ReleaseTargetType Type, string Key), ReleaseSummary>> SummariesAsync(
        IReadOnlyList<(ReleaseTargetType Type, string Key)> targets, CancellationToken ct = default) =>
        _repo.SummariesAsync(targets, ct);

    public string CurrentCycle() => _airac.GetCycle(DateTime.UtcNow);

    // Parte dal ciclo SUCCESSIVO: il corrente si pubblica con "Pubblica ora". Salta il primo (corrente) di NextCycles.
    public IReadOnlyList<AiracCycleInfo> UpcomingCycles(int count) =>
        _airac.NextCycles(DateTime.UtcNow, count + 1).Skip(1).ToList();

    public async Task PublishAsync(ReleaseTargetType type, string key, string releaseCycle, string? note, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(type, key, ct);
        var effectiveUtc = _airac.EffectiveUtcForCycle(releaseCycle);
        await SnapshotAndSaveAsync(type, key, releaseCycle, effectiveUtc, note, ct);
    }

    /// <summary>
    /// Pubblicazione immediata. <b>Tutta dentro una transazione</b>: sono tre scritture distinte, e uno stato
    /// intermedio committato è incoerente in modo vistoso — una release pubblicata di un documento la cui
    /// bozza non è stata promossa, cioè la pagina pubblica che mostra il nuovo e l'editor che mostra il
    /// vecchio. È l'operazione più importante che l'applicazione compie, ed era l'unica senza rete.
    ///
    /// <para><see cref="IUnitOfWork"/> esisteva già ed era usato in due soli posti. Si occupa anche del caso
    /// spinoso: su Neon la strategia di retry rifiuta le transazioni aperte a mano, quindi il blocco va
    /// dentro <c>CreateExecutionStrategy</c> — e al retry il change-tracker va azzerato, o le entità del
    /// tentativo fallito rientrano insieme a quelle del nuovo. Entrambe le cose sono in <c>EfUnitOfWork</c>.</para>
    ///
    /// <para>⚠️ L'autorizzazione resta <b>fuori</b> dalla transazione: negare un permesso non è una scrittura
    /// da annullare, e tenerla dentro significherebbe aprire una transazione anche per rifiutare.</para>
    /// </summary>
    public async Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(type, key, ct);
        var now = DateTime.UtcNow;
        var cycle = _airac.GetCycle(now);

        await _uow.ExecuteInTransactionAsync(async token =>
        {
            await SnapshotAndSaveAsync(type, key, cycle, now, note, token);
            // Pubblicazione IMMEDIATA (review): promuove anche la bozza a versione pubblicata, così lo stato del
            // documento e quello della release restano allineati (la pill dell'editor, la storia versioni, il diff).
            // La VISIBILITÀ pubblica non dipende più da questo: dal doc 10 §S6b è la release effettiva a decidere, e
            // il fallback live del viewer non c'è più — questo commento diceva ancora il contrario.
            // Le release SCHEDULATE (PublishAsync, ciclo futuro) NON promuovono: restano solo snapshot per il ciclo.
            await _repo.PublishWorkingVersionAsync(type, key, _authz.CurrentUserId ?? 0, cycle, token);
            // Retention versioni: DOPO la promozione (che archivia la precedente), così il conteggio Archived include la
            // versione appena archiviata → cap esatto, non N+1. Lo scheduled non promuove/archivia → non serve qui.
            await PruneArchivedVersionsForTargetAsync(type, key, token);
        }, ct);
    }

    public async Task<int> BackfillMissingReleasesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var cycle = _airac.GetCycle(now);
        var count = 0;
        foreach (var d in await _admin.ListAsync(ct))
        {
            if (!d.IsPublished || d.IsHidden) continue;   // solo i pubblicati, non nascosti
            if (await _repo.GetEffectiveAsync(d.ReleaseTarget, d.ReleaseKey, now, ct) is not null) continue;   // già coperto → idempotente

            // Riusa il path di cattura (§3d); tollera i documenti senza contenuto (null) senza esplodere.
            var finalJson = await BuildSnapshotJsonAsync(d.ReleaseTarget, d.ReleaseKey, cycle, ct);
            if (finalJson is null) continue;
            await _repo.SaveReleaseAsync(d.ReleaseTarget, d.ReleaseKey, cycle, now,
                finalJson, createdByUserId: 0, note: "backfill migrazione A (doc 10)", ct);
            count++;
        }
        return count;
    }

    public async Task CancelReleaseAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct)
            ?? throw new Aor.ValidationException("Release inesistente.");
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);
        await _repo.CancelAsync(releaseId, ct);
    }

    public async Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct);
        if (rel is null) return ReleaseDiff.Empty;

        // Baseline = release in vigore ORA per lo stesso bersaglio, escludendo quella in esame.
        var eff = await _repo.GetEffectiveAsync(rel.TargetType, rel.TargetKey, DateTime.UtcNow, ct);
        var baseline = (eff is not null && eff.Id != rel.Id) ? eff : null;

        var cur = Signature(rel.PayloadJson);
        var prev = baseline is null ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                                    : Signature(baseline.PayloadJson);

        var rows = new List<ReleaseDiffRow>();
        foreach (var kv in cur.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!prev.TryGetValue(kv.Key, out var p))
                rows.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Added, null, kv.Value));
            else if (p != kv.Value)
                rows.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Modified, p, kv.Value));
        }
        foreach (var kv in prev.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            if (!cur.ContainsKey(kv.Key))
                rows.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Removed, kv.Value, null));

        // Niente frasi in Application: il ciclo di confronto (o la sua assenza) lo formatta la UI.
        return new ReleaseDiff(baseline is not null, baseline?.ReleaseAiracCycle, rows);
    }

    public async Task<IReadOnlyList<ReleaseDiffRow>> DriftFromEffectiveAsync(
        ReleaseTargetType type, string key, CancellationToken ct = default)
    {
        var effettiva = await _repo.GetEffectiveAsync(type, key, DateTime.UtcNow, ct);
        if (effettiva is null) return Array.Empty<ReleaseDiffRow>();

        // Lo snapshot che si otterrebbe pubblicando ADESSO. Stesso identico percorso della pubblicazione
        // vera (§3d): se divergessero, la deriva segnalerebbe differenze che al momento di pubblicare non
        // esistono — o, peggio, tacerebbe su quelle che esistono.
        var oggiJson = await BuildSnapshotJsonAsync(type, key, _airac.GetCycle(DateTime.UtcNow), ct);
        if (oggiJson is null) return Array.Empty<ReleaseDiffRow>();

        var oggi = Signature(oggiJson);
        var pubblicata = Signature(effettiva.PayloadJson);

        var righe = new List<ReleaseDiffRow>();
        foreach (var kv in oggi.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!pubblicata.TryGetValue(kv.Key, out var p))
                righe.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Added, null, kv.Value));
            else if (p != kv.Value)
                righe.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Modified, p, kv.Value));
        }
        foreach (var kv in pubblicata.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            if (!oggi.ContainsKey(kv.Key))
                righe.Add(new ReleaseDiffRow(kv.Key, ReleaseChangeKind.Removed, kv.Value, null));

        return righe;
    }

    public async Task<ReleasePreview?> GetPreviewAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct);
        if (rel is null) return null;
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);

        // Post-08 tutti i tipi condividono DocReleasePayload → deserializzazione unica, nessuno switch per-tipo.
        RawDocument? doc = null;
        try { doc = JsonSerializer.Deserialize<DocReleasePayload>(rel.PayloadJson)?.Doc; }
        catch (JsonException) { }
        return new ReleasePreview(rel.TargetType, rel.TargetKey, rel.ReleaseAiracCycle, doc);
    }

    public async Task<ReleaseLocation?> GetLocationAsync(int releaseId, CancellationToken ct = default)
    {
        var rel = await _repo.GetByIdAsync(releaseId, ct);
        if (rel is null) return null;
        await EnsureCanEditAsync(rel.TargetType, rel.TargetKey, ct);
        var acc = await _repo.GetAuthAccCodeAsync(rel.TargetType, rel.TargetKey, ct)
            ?? throw new Aor.ValidationException("Bersaglio della release inesistente.");
        return new ReleaseLocation(rel.TargetType, rel.TargetKey, rel.ReleaseAiracCycle, acc);
    }

    /// <summary>Firma editoriale di un payload: voce (sezione/blocco) → conteggio elementi. Base del diff.
    /// Post-08 tutti i tipi sono su DocReleasePayload → firma unica, nessuno switch per-tipo.</summary>
    private static Dictionary<string, int> Signature(string payloadJson)
    {
        var sig = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var p = JsonSerializer.Deserialize<DocReleasePayload>(payloadJson);
            if (p?.Doc?.Roots is not null) FlattenSections(p.Doc.Roots, "", sig);
        }
        catch (JsonException) { }
        return sig;
    }

    private static void FlattenSections(IReadOnlyList<RawSection> sections, string prefix, Dictionary<string, int> sig)
    {
        foreach (var s in sections)
        {
            var label = prefix.Length == 0 ? s.Title : $"{prefix} / {s.Title}";
            sig[label] = s.Blocks.Count;
            if (s.Children.Count > 0) FlattenSections(s.Children, label, sig);
        }
    }

    private async Task SnapshotAndSaveAsync(ReleaseTargetType type, string key, string cycle, DateTime effectiveUtc, string? note, CancellationToken ct)
    {
        var finalJson = await BuildSnapshotJsonAsync(type, key, cycle, ct)
            ?? throw new Aor.ValidationException("Nessun contenuto da pubblicare: crea prima il documento (bozza).");
        await _repo.SaveReleaseAsync(type, key, cycle, effectiveUtc, finalJson, _authz.CurrentUserId ?? 0, note, ct);
        // Retention per-publish (release Superseded): sia per l'immediato sia per lo schedulato. Le versioni Archived
        // si potano solo dopo la promozione della bozza (PublishNowAsync) → vedi PruneArchivedVersionsForTargetAsync.
        await _repo.PruneReleasesAsync(type, key, KeepSupersededFromUtc(), ct);
    }

    public async Task<int> PruneAllAsync(CancellationToken ct = default)
    {
        var removed = 0;
        var keepFrom = KeepSupersededFromUtc();
        foreach (var d in await _admin.ListAsync(ct))
        {
            await _repo.PruneReleasesAsync(d.ReleaseTarget, d.ReleaseKey, keepFrom, ct);
            if (d.DocumentId is int docId)
                removed += await _editing.PruneArchivedVersionsAsync(docId, _retention.KeepArchivedVersionsPerDocument, ct);
        }
        return removed;
    }

    // Potatura versioni Archived oltre N del bersaglio. Va invocata DOPO l'archiviazione della versione appena
    // pubblicata (PublishNowAsync), altrimenti il conteggio è di uno in meno e resta N+1.
    private async Task PruneArchivedVersionsForTargetAsync(ReleaseTargetType type, string key, CancellationToken ct)
    {
        var docId = await _targets.For(type).ResolveDocumentIdAsync(key, ct);
        if (docId is int id)
            await _editing.PruneArchivedVersionsAsync(id, _retention.KeepArchivedVersionsPerDocument, ct);
    }

    // Soglia temporale: data efficace del ciclo AIRAC corrente meno N cicli (28 giorni cadauno). Le release Superseded
    // con data efficace anteriore vengono potate.
    private DateTime KeepSupersededFromUtc() =>
        _airac.EffectiveUtcForCycle(_airac.GetCycle(DateTime.UtcNow))
              .AddDays(-_retention.KeepSupersededWithinCycles * 28);

    // Snapshot totale (doc 10 §3c): struttura congelata + OUTPUT delle sezioni derivate in modalità Frozen, così il
    // pubblico vede una fotografia completa (le sezioni Live restano fuori: il viewer le deriva sul momento). Ritorna il
    // JSON del payload pronto per SaveReleaseAsync, o null se il documento non ha contenuto (nessuna versione di lavoro).
    private async Task<string?> BuildSnapshotJsonAsync(ReleaseTargetType type, string key, string cycle, CancellationToken ct)
    {
        var json = await _repo.SnapshotWorkingAsync(type, key, cycle, ct);
        if (json is null) return null;
        var payload = JsonSerializer.Deserialize<DocReleasePayload>(json)!;

        // ⚠️ Il congelamento chiede le shape IN VIGORE AL CICLO DI QUESTA RELEASE, non le più recenti: il
        // sectorfile lo scriviamo in anticipo, quindi in catalogo può già esserci il confine del ciclo
        // prossimo. Pubblicando per il ciclo corrente esce la geometria vecchia; pubblicando in anticipo
        // PER il ciclo prossimo — che è quel che si fa preparando un AIRAC — esce quella nuova. Vedi
        // ShapeAiracGate e docs/feature/2026-08-26-shape-dal-sectorfile.md §3.
        IReadOnlyDictionary<int, string> frozen;
        using (_shapeCycle?.Capturing(cycle))
            frozen = await _frozen.CaptureAsync(type, key, payload.Doc, ct);
        foreach (var kv in frozen) payload.FrozenSections[kv.Key] = kv.Value;
        return JsonSerializer.Serialize(payload);
    }

    private async Task EnsureCanEditAsync(ReleaseTargetType type, string key, CancellationToken ct)
    {
        var acc = await _repo.GetAuthAccCodeAsync(type, key, ct)
            ?? throw new Aor.ValidationException("Bersaglio della release inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }
}
