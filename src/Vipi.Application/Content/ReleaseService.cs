using System.Text.Json;
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

    /// <summary>Annulla una release (per Id). Authz sull'ACC del bersaglio.</summary>
    Task CancelReleaseAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Riepilogo differenze di una release rispetto a quella in vigore (o allo stato pubblicato/live).</summary>
    Task<ReleaseDiff> DiffAsync(int releaseId, CancellationToken ct = default);

    /// <summary>Anteprima di una release: metadati + <see cref="RawDocument"/> del payload per i tipi doc-based
    /// (vLOA/aeroporto); Doc null per ACC/APP (anteprima strutturale non resa qui). Authz ACC.</summary>
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
}

/// <inheritdoc cref="IReleaseService"/>
public sealed class ReleaseService : IReleaseService
{
    private readonly IReleaseRepository _repo;
    private readonly IEditAuthorizationService _authz;
    private readonly IAiracService _airac;
    private readonly IFrozenSectionRegistry _frozen;

    public ReleaseService(IReleaseRepository repo, IEditAuthorizationService authz, IAiracService airac, IFrozenSectionRegistry frozen)
    {
        _repo = repo;
        _authz = authz;
        _airac = airac;
        _frozen = frozen;
    }

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

    public async Task PublishNowAsync(ReleaseTargetType type, string key, string? note, CancellationToken ct = default)
    {
        await EnsureCanEditAsync(type, key, ct);
        var now = DateTime.UtcNow;
        var cycle = _airac.GetCycle(now);
        await SnapshotAndSaveAsync(type, key, cycle, now, note, ct);
        // Pubblicazione IMMEDIATA (review): promuove anche la bozza a versione pubblicata, così il documento è visibile
        // nelle liste pubbliche (gate su Status==Published) e nel fallback del viewer, non solo via snapshot di release.
        // Le release SCHEDULATE (PublishAsync, ciclo futuro) NON promuovono: restano solo snapshot per il ciclo.
        await _repo.PublishWorkingVersionAsync(type, key, _authz.CurrentUserId ?? 0, cycle, ct);
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
                rows.Add(new ReleaseDiffRow(kv.Key, "Aggiunta", $"{kv.Value} elementi"));
            else if (p != kv.Value)
                rows.Add(new ReleaseDiffRow(kv.Key, "Modificata", $"{p} → {kv.Value} elementi"));
        }
        foreach (var kv in prev.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            if (!cur.ContainsKey(kv.Key))
                rows.Add(new ReleaseDiffRow(kv.Key, "Rimossa", $"{kv.Value} elementi"));

        var baselineLabel = baseline is null ? "stato attuale (nessuna release in vigore)" : $"AIRAC {baseline.ReleaseAiracCycle}";
        return new ReleaseDiff(baseline is not null, baselineLabel, rows);
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
        var json = await _repo.SnapshotWorkingAsync(type, key, cycle, ct)
            ?? throw new Aor.ValidationException("Nessun contenuto da pubblicare: crea prima il documento (bozza).");

        // Cattura totale (doc 10 §3c): congela anche l'OUTPUT delle sezioni derivate in modalità Frozen, così il
        // pubblico vede una fotografia completa. Le sezioni Live restano fuori (il viewer le deriva sul momento).
        var payload = JsonSerializer.Deserialize<DocReleasePayload>(json)!;
        var frozen = await _frozen.CaptureAsync(type, key, payload.Doc, ct);
        foreach (var kv in frozen) payload.FrozenSections[kv.Key] = kv.Value;
        var finalJson = JsonSerializer.Serialize(payload);

        var userId = _authz.CurrentUserId ?? 0;
        await _repo.SaveReleaseAsync(type, key, cycle, effectiveUtc, finalJson, userId, note, ct);
    }

    private async Task EnsureCanEditAsync(ReleaseTargetType type, string key, CancellationToken ct)
    {
        var acc = await _repo.GetAuthAccCodeAsync(type, key, ct)
            ?? throw new Aor.ValidationException("Bersaglio della release inesistente.");
        await _authz.EnsureCanEditAccAsync(acc, ct);
    }
}
