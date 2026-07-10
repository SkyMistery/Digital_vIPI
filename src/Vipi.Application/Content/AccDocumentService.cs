using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Authoring della vIPI ACC sul modello unificato <c>Document</c> (doc refactor 08e-acc, strategia A/Opzione A). Sostituirà
/// il vecchio <c>AccProfileService</c>/<c>AccProfile</c> (rimossi in 08i): i blocchi vivono come sezioni radice del Document
/// (una per blocco) con le sezioni-catalogo come figlie; il metadata del blocco (natura/membri/override) nel <c>BodyJson</c>
/// del blocco proprio della sezione-blocco, config/aree/separations/vfr nei <c>BodyJson</c> delle figlie keyed. Le derivazioni
/// (freq/coord/AoR/config-table) restano calcolate live: si passa il blocco assemblato a <see cref="IAccProfileService"/>.
/// Questo service possiede solo il ciclo storage (Ensure/assembla/salva); l'autorizzazione è ACC-scoped.
/// </summary>
public interface IAccDocumentService
{
    /// <summary>Idempotente: garantisce il Document vIPI ACC (creato greenfield col blocco Aerovia di default se mancante,
    /// chiavizzato sul settore CTR radice primario) e ne ritorna l'Id. ACC-gated.</summary>
    Task<int> EnsureAsync(string accCode, CancellationToken ct = default);

    /// <summary>Identità del Document ACC (settore radice, codice/nome ACC, DocumentId se migrato). Null se l'ACC non esiste o non ha radici CTR.</summary>
    Task<AccDocumentIdentity?> GetIdentityAsync(string accCode, CancellationToken ct = default);

    /// <summary>Carica la vIPI ACC dalla versione di lavoro (bozza se esiste, sennò la pubblicata) assemblando i blocchi. ACC-gated; garantisce il Document.</summary>
    Task<AccDocumentModel> LoadForEditAsync(string accCode, CancellationToken ct = default);

    /// <summary>Vista PUBBLICA: assembla i blocchi dallo snapshot della release AIRAC in vigore (se esiste), altrimenti
    /// dalla versione pubblicata del Document. Se l'ACC non ha ancora un Document (o versione pubblicata), ritorna un
    /// blocco Aerovia vuoto di default così le sezioni derivate (freq/AoR/coord) restano visibili dai cataloghi. Non
    /// gated, non crea il Document. Null solo se l'ACC non esiste.</summary>
    Task<AccDocumentModel?> LoadForViewAsync(string accCode, CancellationToken ct = default);

    /// <summary>Anteprima di una specifica release ACC: assembla i blocchi CONGELATI dallo snapshot (DocReleasePayload) +
    /// ciclo AIRAC. Gated can-edit ACC; verifica che la release sia dell'ACC indicato. Null se non corrisponde. Doc 08e-acc.</summary>
    Task<AccReleaseView?> LoadForReleaseAsync(string accCode, int releaseId, CancellationToken ct = default);

    /// <summary>Salva il metadata del blocco (natura/membri/override) nel <c>BodyJson</c> del blocco proprio della sezione-blocco. ACC-gated.</summary>
    Task SaveBlockMetaAsync(string accCode, int blockSectionId, AccBlockMeta meta, CancellationToken ct = default);

    /// <summary>Salva le configurazioni di un blocco nel <c>BodyJson</c> della sua sezione figlia <c>configurations</c>. ACC-gated.</summary>
    Task SaveConfigurationsAsync(string accCode, int configSectionId, IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default);

    /// <summary>Salva le aree regolamentate attaccate (id IVAO) nel <c>BodyJson</c> della sezione figlia <c>regulated</c>. ACC-gated.</summary>
    Task SaveRegulatedAsync(string accCode, int regulatedSectionId, IReadOnlyList<string> attachedIds, CancellationToken ct = default);

    /// <summary>Salva le righe Separazioni nel <c>BodyJson</c> della sezione figlia <c>separations</c>. ACC-gated.</summary>
    Task SaveSeparationsAsync(string accCode, int separationsSectionId, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default);

    /// <summary>Salva il contenuto VFR nel <c>BodyJson</c> della sezione figlia <c>vfr</c>. ACC-gated.</summary>
    Task SaveVfrAsync(string accCode, int vfrSectionId, AppVfrContent content, CancellationToken ct = default);

    /// <summary>Aggiunge un blocco gruppo APP (sezione radice + sezioni-catalogo AccAppBlock) alla versione bozza. ACC-gated. Ritorna l'Id della sezione-blocco.</summary>
    Task<int> AddGroupAsync(string accCode, int versionId, string title, CancellationToken ct = default);

    /// <summary>Elimina un blocco (sezione radice + sottoalbero) dalla versione bozza. ACC-gated.</summary>
    Task RemoveGroupAsync(string accCode, int blockSectionId, CancellationToken ct = default);
}

/// <inheritdoc cref="IAccDocumentService"/>
public sealed class AccDocumentService : IAccDocumentService
{
    private readonly IAccProfileRepository _repo;
    private readonly IEditingRepository _editing;
    private readonly IEditAuthorizationService _authz;
    private readonly IReleaseRepository _releases;

    public AccDocumentService(IAccProfileRepository repo, IEditingRepository editing, IEditAuthorizationService authz,
        IReleaseRepository releases)
    {
        _repo = repo;
        _editing = editing;
        _authz = authz;
        _releases = releases;
    }

    private static string Norm(string s) => (s ?? "").Trim().ToUpperInvariant();

    // Sezioni "live" della vIPI ACC (derivate o rese live da componenti dedicati): ricevono un blocco placeholder alla
    // creazione così restano visibili nel viewer anche senza contenuto memorizzato. Union dei due profili ACC.
    private static readonly string[] LiveKeys =
        { "separations", "aor", "frequencies", "minima", "vfr", "coordination" };

    public Task<AccDocumentIdentity?> GetIdentityAsync(string accCode, CancellationToken ct = default) =>
        _repo.ResolveAccDocumentIdentityAsync(Norm(accCode), ct);

    public async Task<int> EnsureAsync(string accCode, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        var id = await _repo.ResolveAccDocumentIdentityAsync(accCode, ct)
            ?? throw new Aor.ValidationException($"ACC {accCode} inesistente o senza settori CTR.");
        if (id.DocumentId is int existing) return existing;   // già migrato

        await _authz.EnsureCanEditAccAsync(accCode, ct);

        // Struttura di default: un solo blocco Aerovia con le sezioni del catalogo. I gruppi APP si aggiungono dall'editor.
        var aerovia = new VipiBlockSpec("aerovia", "Settori di aerovia",
            SectionCatalog.For(SectionProfile.AccAerovia).Select(d => (d.Key, d.Title)).ToList());

        return await _editing.EnsureVipiDocumentTreeAsync(id.SectorId, $"vIPI {id.AccName}", Language.It,
            new[] { aerovia }, _authz.CurrentUserId ?? 0, LiveKeys, ct);
    }

    public async Task<AccDocumentModel> LoadForEditAsync(string accCode, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        var id = await _repo.ResolveAccDocumentIdentityAsync(accCode, ct)
            ?? throw new Aor.ValidationException($"ACC {accCode} inesistente o senza settori CTR.");
        await _authz.EnsureCanEditAccAsync(accCode, ct);

        var docId = await EnsureAsync(accCode, ct);
        var doc = await _editing.LoadForEditAsync(docId, ct)
            ?? throw new Aor.ValidationException($"vIPI ACC {accCode} senza versione di lavoro.");

        var blocks = AccDocumentAssembler.Assemble(doc);
        return new AccDocumentModel(doc.DocumentId, doc.VersionId, doc.IsEditable, accCode, id.AccName, blocks);
    }

    public async Task<AccDocumentModel?> LoadForViewAsync(string accCode, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        var id = await _repo.ResolveAccDocumentIdentityAsync(accCode, ct);
        if (id is null) return null;   // ACC inesistente

        // Se esiste una release AIRAC in vigore per questo ACC, il pubblico vede i blocchi CONGELATI dello snapshot
        // (le sezioni derivate — freq/AoR/coord — restano live coi cataloghi correnti). Doc 08e-acc.
        var rel = await _releases.GetEffectiveAsync(ReleaseTargetType.AccVipi, $"{accCode}|{id.RootCallsign}", DateTime.UtcNow, ct);
        if (rel is not null && DeserializeSnapshot(rel.PayloadJson) is { } snapRaw)
        {
            var blocks = AccDocumentAssembler.Assemble(snapRaw);
            return new AccDocumentModel(id.DocumentId ?? 0, 0, IsDraft: false, accCode, id.AccName, blocks);
        }

        if (id.DocumentId is int docId && await _editing.LoadForViewAsync(docId, ct) is { } doc)
        {
            var docBlocks = AccDocumentAssembler.Assemble(doc);
            return new AccDocumentModel(doc.DocumentId, doc.VersionId, IsDraft: false, accCode, id.AccName, docBlocks);
        }

        // Non ancora migrato/pubblicato: blocco Aerovia vuoto di default (le derivate restano live dai cataloghi).
        var aerovia = new AccBlock
        {
            Key = "aerovia", Kind = AccBlockKind.Aerovia, Title = "Settori di aerovia",
            SectionOrder = SectionCatalog.For(SectionProfile.AccAerovia).Select(d => d.Key).ToList(),
        };
        var synthetic = new AccAssembledBlock(0, aerovia, new Dictionary<string, int>());
        return new AccDocumentModel(0, 0, IsDraft: false, accCode, id.AccName, new[] { synthetic });
    }

    public async Task<AccReleaseView?> LoadForReleaseAsync(string accCode, int releaseId, CancellationToken ct = default)
    {
        accCode = Norm(accCode);
        await _authz.EnsureCanEditAccAsync(accCode, ct);
        var rel = await _releases.GetByIdAsync(releaseId, ct);
        if (rel is null || rel.TargetType != ReleaseTargetType.AccVipi) return null;
        if (!rel.TargetKey.StartsWith(accCode + "|", StringComparison.OrdinalIgnoreCase)) return null;
        if (DeserializeSnapshot(rel.PayloadJson) is not { } raw) return null;

        var name = (await _repo.ResolveAccDocumentIdentityAsync(accCode, ct))?.AccName ?? accCode;
        var blocks = AccDocumentAssembler.Assemble(raw);
        var data = new AccProfileData { AccCode = accCode, AccName = name, Blocks = blocks.Select(b => b.Block).ToList() };
        return new AccReleaseView(data, rel.ReleaseAiracCycle);
    }

    // Snapshot release ACC = DocReleasePayload (ramo Document, doc 08e-acc): estrae il RawDocument congelato.
    private static RawDocument? DeserializeSnapshot(string payloadJson)
    {
        try { return JsonSerializer.Deserialize<DocReleasePayload>(payloadJson)?.Doc; }
        catch (JsonException) { return null; }
    }

    // --- Salvataggi editoriali by-section (ACC-gated). Il BodyJson vive nel blocco della sezione indicata. ---

    public Task SaveBlockMetaAsync(string accCode, int blockSectionId, AccBlockMeta meta, CancellationToken ct = default) =>
        SaveJsonAsync(accCode, blockSectionId, meta, ct);

    public Task SaveConfigurationsAsync(string accCode, int configSectionId, IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default) =>
        SaveJsonAsync(accCode, configSectionId, (configs?.Count ?? 0) == 0 ? null : configs, ct);

    public Task SaveRegulatedAsync(string accCode, int regulatedSectionId, IReadOnlyList<string> attachedIds, CancellationToken ct = default) =>
        SaveJsonAsync(accCode, regulatedSectionId, (attachedIds?.Count ?? 0) == 0 ? null : attachedIds, ct);

    public Task SaveSeparationsAsync(string accCode, int separationsSectionId, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default)
    {
        var clean = (rows ?? Array.Empty<AppSeparationRow>())
            .Select(r => new AppSeparationRow((r.Vertical ?? "").Trim(), (r.Lateral ?? "").Trim(),
                string.IsNullOrWhiteSpace(r.Applicability) ? null : r.Applicability!.Trim()))
            .ToList();
        return SaveJsonAsync(accCode, separationsSectionId, clean.Count == 0 ? null : clean, ct);
    }

    public Task SaveVfrAsync(string accCode, int vfrSectionId, AppVfrContent content, CancellationToken ct = default)
    {
        var empty = content is null || (string.IsNullOrWhiteSpace(content.Intro) && content.Rows.Count == 0);
        return SaveJsonAsync(accCode, vfrSectionId, empty ? null : content, ct);
    }

    // Serializza (null/vuoto azzera) e scrive il BodyJson della sezione, previa autorizzazione ACC.
    private async Task SaveJsonAsync(string accCode, int sectionId, object? value, CancellationToken ct)
    {
        await _authz.EnsureCanEditAccAsync(Norm(accCode), ct);
        var json = value is null ? null : System.Text.Json.JsonSerializer.Serialize(value);
        await _editing.SaveSectionBlockJsonBySectionAsync(sectionId, json, _authz.CurrentUserId ?? 0, ct);
    }

    // --- Ops strutturali sui blocchi (ACC-gated). Operano sulla versione bozza gestita dall'editor. ---

    public async Task<int> AddGroupAsync(string accCode, int versionId, string title, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(Norm(accCode), ct);
        var block = new VipiBlockSpec("appgroup", string.IsNullOrWhiteSpace(title) ? "Nuovo gruppo APP" : title.Trim(),
            SectionCatalog.For(SectionProfile.AccAppBlock).Select(d => (d.Key, d.Title)).ToList());
        var blockSectionId = await _editing.AddBlockToVersionAsync(versionId, block, LiveKeys, ct);

        // Semina un blockmeta con chiave unica (grp:{guid}) così i blocchi non collidono (anchor/dizionari).
        var meta = new AccBlockMeta { Key = "grp:" + Guid.NewGuid().ToString("N")[..8], Kind = AccBlockKind.AppGroup };
        await _editing.SaveSectionBlockJsonBySectionAsync(blockSectionId, System.Text.Json.JsonSerializer.Serialize(meta),
            _authz.CurrentUserId ?? 0, ct);
        return blockSectionId;
    }

    public async Task RemoveGroupAsync(string accCode, int blockSectionId, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(Norm(accCode), ct);
        await _editing.DeleteSectionAsync(blockSectionId, ct);
    }
}
