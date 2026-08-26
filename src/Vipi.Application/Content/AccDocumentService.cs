using System.Text.Json;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Authoring della vIPI ACC sul modello unificato <c>Document</c> (doc refactor 08e-acc, strategia A/Opzione A).
/// Rimpiazza lo storage profile eliminato in 08i (tabelle <c>AccProfiles</c> droppate): i blocchi vivono come sezioni
/// radice del Document (una per blocco) con le sezioni-catalogo come figlie; il metadata del blocco (natura/membri/override)
/// nel <c>BodyJson</c> del blocco proprio della sezione-blocco, config/aree/separations/vfr nei <c>BodyJson</c> delle figlie
/// keyed. Le derivazioni (freq/coord/AoR/config-table) restano calcolate live da <see cref="IAccDerivationService"/>, a cui
/// si passa il blocco assemblato. Questo service possiede solo il ciclo storage (Ensure/assembla/salva); authz ACC-scoped.
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

    /// <summary>Vista PUBBLICA: assembla i blocchi dallo snapshot della release AIRAC in vigore. Non gated, non crea
    /// il Document. Null se l'ACC non esiste, se non c'è release effettiva (doc 10 §3f/§S6b: visibilità pubblica =
    /// release effettiva, nessun fallback live) o se il documento è nascosto dall'admin — lo stesso gate
    /// <c>IsHidden</c> che gli altri tipi applicano nel predicato di <c>EfContentRepository.LoadVipiAsync</c>.</summary>
    Task<AccDocumentModel?> LoadForViewAsync(string accCode, CancellationToken ct = default);

    /// <summary>Anteprima di una specifica release ACC: assembla i blocchi CONGELATI dallo snapshot (DocReleasePayload) +
    /// ciclo AIRAC. Gated can-edit ACC; verifica che la release sia dell'ACC indicato. Null se non corrisponde. Doc 08e-acc.</summary>
    Task<AccReleaseView?> LoadForReleaseAsync(string accCode, int releaseId, CancellationToken ct = default);

    /// <summary>Salva il metadata del blocco (natura/membri/override) nel <c>BodyJson</c> del blocco proprio della sezione-blocco. ACC-gated.</summary>
    Task SaveBlockMetaAsync(string accCode, int blockSectionId, AccBlockMeta meta, CancellationToken ct = default);

    /// <summary>Salva le configurazioni di un blocco nel <c>BodyJson</c> della sua sezione figlia <c>configurations</c>. ACC-gated.</summary>
    Task SaveConfigurationsAsync(string accCode, int configSectionId, IReadOnlyList<AccConfiguration> configs, CancellationToken ct = default);

    /// <summary>Salva la selezione aree regolamentate (proprio ACC auto/manuale + extra) nel <c>BodyJson</c> della sezione
    /// figlia <c>regulated</c>. «Puro automatico senza extra» azzera il BodyJson (resta dinamico). ACC-gated.</summary>
    Task SaveRegulatedAsync(string accCode, int regulatedSectionId, RegulatedSelection selection, CancellationToken ct = default);

    /// <summary>Salva la personalizzazione AoR (shape extra + override colore per settore) nel <c>BodyJson</c> della
    /// sezione figlia <c>aor</c>. Vuota (nessun extra, nessun colore) azzera il BodyJson. ACC-gated.</summary>
    Task SaveAorCustomizationAsync(string accCode, int aorSectionId, AorExtraShapes data, CancellationToken ct = default);

    /// <summary>Salva le righe Separazioni nel <c>BodyJson</c> della sezione figlia <c>separations</c>. ACC-gated.</summary>
    Task SaveSeparationsAsync(string accCode, int separationsSectionId, IReadOnlyList<AppSeparationRow> rows, CancellationToken ct = default);

    /// <summary>Salva il contenuto VFR nel <c>BodyJson</c> della sezione figlia <c>vfr</c>. ACC-gated.</summary>
    Task SaveVfrAsync(string accCode, int vfrSectionId, AppVfrContent content, CancellationToken ct = default);

    /// <summary>Aggiunge un blocco gruppo APP (sezione radice + sezioni-catalogo AccAppBlock) alla versione bozza. ACC-gated. Ritorna l'Id della sezione-blocco.</summary>
    Task<int> AddGroupAsync(string accCode, int versionId, string title, CancellationToken ct = default);

    /// <summary>Elimina un blocco (sezione radice + sottoalbero) dalla versione bozza. ACC-gated.</summary>
    Task RemoveGroupAsync(string accCode, int blockSectionId, CancellationToken ct = default);

    /// <summary>
    /// Sposta un GRUPPO APP di un posto (direction -1 su, +1 giù) fra i blocchi del documento. ACC-gated.
    /// <para>
    /// ⚠️ Il blocco <b>Aerovia resta in testa</b>: non si sposta e nessun gruppo gli passa sopra (decisione del
    /// committente, 26 agosto). Una mossa che violerebbe la regola — o che uscirebbe dall'elenco — non fa niente:
    /// l'editor non mostra la freccia, e questa è la rete per chi arrivasse per un'altra strada.
    /// </para>
    /// </summary>
    Task MoveGroupAsync(string accCode, int blockSectionId, int direction, CancellationToken ct = default);
}

/// <inheritdoc cref="IAccDocumentService"/>
public sealed class AccDocumentService : IAccDocumentService
{
    private readonly IAccDerivationRepository _repo;
    private readonly IEditingRepository _editing;
    private readonly IEditAuthorizationService _authz;
    private readonly IReleaseRepository _releases;

    public AccDocumentService(IAccDerivationRepository repo, IEditingRepository editing, IEditAuthorizationService authz,
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
    // «minima» non c'è più (doc 13 §3b): è una sezione editoriale come le altre e un blocco tabella vuoto
    // le darebbe un editor di tabella che nessuno ha chiesto.
    private static readonly string[] LiveKeys =
        { "separations", "aor", "frequencies", "vfr", "coordination" };

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

        // Documento nascosto dall'admin ⇒ invisibile al pubblico, PRIMA di guardare la release. Gli altri tipi
        // hanno questo gate nel predicato di caricamento (EfContentRepository.LoadVipiAsync, `!d.IsHidden`); qui
        // mancava, e una vIPI ACC nascosta spariva da landing/ricerca ma restava servita all'URL diretto.
        if (id.IsDocumentHidden) return null;

        // Visibilità pubblica = esiste una release AIRAC in vigore (doc 10 §3f/§S6b, uniforme alle altre famiglie):
        // il pubblico vede i blocchi CONGELATI dello snapshot (le derivate — freq/AoR/coord — restano live). Senza
        // release effettiva la vIPI ACC è invisibile (null) — rimosso il fallback storico alla versione pubblicata
        // live e il guscio sintetico vuoto. La migrazione A (backfill al boot) garantisce una release ai Published.
        var rel = await _releases.GetEffectiveAsync(ReleaseTargetType.AccVipi, $"{accCode}|{id.RootCallsign}", DateTime.UtcNow, ct);
        if (rel is not null && DeserializeSnapshot(rel.PayloadJson) is { } snapRaw)
        {
            var blocks = AccDocumentAssembler.Assemble(snapRaw);
            return new AccDocumentModel(id.DocumentId ?? 0, 0, IsDraft: false, accCode, id.AccName, blocks,
                rel.ReleaseAiracCycle);
        }

        return null;
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
        var data = new AccVipiData { AccCode = accCode, AccName = name, Blocks = blocks.Select(b => b.Block).ToList() };
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

    public Task SaveRegulatedAsync(string accCode, int regulatedSectionId, RegulatedSelection selection, CancellationToken ct = default)
    {
        // Puro automatico senza extra = default dinamico → azzera il BodyJson (nessuno stato persistito).
        var isPureAuto = selection.OwnAuto && selection.OwnIds.Count == 0 && selection.ExtraIds.Count == 0;
        return SaveJsonAsync(accCode, regulatedSectionId, isPureAuto ? null : selection, ct);
    }

    public Task SaveAorCustomizationAsync(string accCode, int aorSectionId, AorExtraShapes data, CancellationToken ct = default)
    {
        var clean = AorCustomizationCleaner.Clean(data);
        var empty = clean.Callsigns.Count == 0 && clean.Colors.Count == 0;
        return SaveJsonAsync(accCode, aorSectionId, empty ? null : clean, ct);
    }

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

    public async Task MoveGroupAsync(string accCode, int blockSectionId, int direction, CancellationToken ct = default)
    {
        await _authz.EnsureCanEditAccAsync(Norm(accCode), ct);

        var docId = await _editing.GetDocumentIdBySectionAsync(blockSectionId, ct);
        if (docId is null) return;
        var doc = await _editing.LoadForEditAsync(docId.Value, ct);
        if (doc is null) return;

        // I blocchi nell'ordine del documento, con la loro natura: la stessa lettura dell'editor e del viewer
        // (l'Aerovia si riconosce dal blockmeta, non dal posto che occupa).
        var blocks = AccDocumentAssembler.Assemble(doc.Sections);
        var i = -1;
        for (var k = 0; k < blocks.Count; k++)
            if (blocks[k].BlockSectionId == blockSectionId) { i = k; break; }
        if (i < 0 || blocks[i].Block.Kind == AccBlockKind.Aerovia) return;

        var j = i + Math.Sign(direction);
        if (j < 0 || j >= blocks.Count || blocks[j].Block.Kind == AccBlockKind.Aerovia) return;

        await _editing.MoveSectionAsync(blockSectionId, direction, ct);
    }
}
