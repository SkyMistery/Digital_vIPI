using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF Core di <see cref="IContentRepository"/>.</summary>
public sealed class EfContentRepository : IContentRepository
{
    private readonly VipiDbContext _db;
    private readonly IReleaseRepository _releases;

    public EfContentRepository(VipiDbContext db, IReleaseRepository releases)
    {
        _db = db;
        _releases = releases;
    }

    public Task<RawDocument?> LoadAccVipiAsync(string accCode, CancellationToken ct = default)
    {
        // Gate su !IsHidden (non su Status==Published): così una release AIRAC effettiva è servita anche se il Document
        // è ancora Draft (release e pubblicazione-versione sono due layer). Senza release, il fallback interno serve solo
        // una versione pubblicata → i doc mai pubblicati e senza release restano vuoti (nessun leak).
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vipi
                 && !d.IsHidden
                 && d.Sectors.Any(s => s.Kind == SectorKind.Acc && s.Acc!.Code == accCode),
            ignoreRelease: false, preferWorking: false, ct);
    }

    public Task<RawDocument?> LoadAirportVipiAsync(string icao, bool ignoreRelease = false, CancellationToken ct = default)
    {
        // ignoreRelease (anteprima bozza, gated all'editor): mostra anche i documenti/aeroporti nascosti dall'admin.
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vipi
                 && (ignoreRelease || !d.IsHidden)
                 && d.Sectors.Any(s => s.Kind == SectorKind.Airport && s.AirportIcao == icao)
                 // Aeroporto nascosto dall'admin ⇒ pagina pubblica inaccessibile (ma visibile in anteprima bozza).
                 && (ignoreRelease || !_db.Airports.Any(a => a.Icao == icao && a.IsHidden)),
            ignoreRelease, preferWorking: false, ct);
    }

    public Task<RawDocument?> LoadAppVipiAsync(string appCallsign, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default)
    {
        var app = (appCallsign ?? "").Trim().ToUpperInvariant();
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vipi
                 && (preferWorking || ignoreRelease || !d.IsHidden)
                 && d.Sectors.Any(s => s.IsPrimary && s.Type == SectorType.App
                        && s.ApproachKind == ApproachKind.Standalone && s.Callsign == app),
            ignoreRelease, preferWorking, ct);
    }

    public Task<RawDocument?> LoadVloaAsync(string accCode, CancellationToken ct = default)
    {
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vloa
                 && !d.IsHidden
                 && d.Parties.Any(pa => pa.Role == PartyRole.Home && pa.Sector!.Acc!.Code == accCode),
            ignoreRelease: false, preferWorking: false, ct);
    }

    public Task<RawDocument?> LoadVloaByIdAsync(int docId, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default)
    {
        // ignoreRelease/preferWorking (anteprima bozza, gated all'editor): mostra anche vLOA nascoste e non pubblicate,
        // usando la versione di lavorazione più recente.
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vloa
                 && (preferWorking || ignoreRelease || !d.IsHidden)
                 && d.Id == docId,
            ignoreRelease, preferWorking, ct);
    }

    public Task<RawDocument?> LoadVloaByPairAsync(string homeAccCode, string foreignAccCode, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default)
    {
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vloa
                 && (preferWorking || ignoreRelease || !d.IsHidden)
                 && d.Parties.Any(pa => pa.Role == PartyRole.Home && pa.Sector!.Acc!.Code == homeAccCode)
                 && d.Parties.Any(pa => pa.Role == PartyRole.Neighbour && pa.Sector!.Acc!.Code == foreignAccCode),
            ignoreRelease, preferWorking, ct);
    }

    private async Task<RawDocument?> LoadVipiAsync(
        System.Linq.Expressions.Expression<Func<Document, bool>> predicate, bool ignoreRelease, bool preferWorking, CancellationToken ct)
    {
        var doc = await _db.Documents
            .AsNoTracking()
            .Where(predicate)
            .FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        // Se il documento ha una release AIRAC effettiva ADESSO, il pubblico vede lo snapshot editoriale congelato
        // (i dati derivati restano live). ignoreRelease=true (anteprima bozza): salta lo snapshot e usa lo stato live.
        ReleaseTargetType? relType = null; string? relKey = null;
        if (!ignoreRelease) (relType, relKey) = await ResolveReleaseTargetAsync(doc, ct);
        if (relType is ReleaseTargetType t && relKey is string key)
        {
            var eff = await _releases.GetEffectiveAsync(t, key, DateTime.UtcNow, ct);
            if (eff is not null)
            {
                var payload = JsonSerializer.Deserialize<DocReleasePayload>(eff.PayloadJson);
                if (payload?.Doc is not null) return payload.Doc;   // AiracCycle già = ciclo di rilascio (fissato allo snapshot)
            }
        }

        // Visibilità pubblica = esiste una release effettiva (doc 10 §3f/§S6b): rimosso il fallback storico che
        // rendeva LIVE la versione pubblicata senza release. Sul path pubblico puro (né anteprima bozza né working)
        // niente release ⇒ invisibile. La migrazione A (backfill al boot) garantisce una release ai Published.
        if (!ignoreRelease && !preferWorking) return null;

        // Da qui in poi SOLO anteprime gated all'editor (ignoreRelease/preferWorking): mostrano lo stato live/bozza.
        if (!preferWorking && doc.Status != DocumentStatus.Published) return null;

        // preferWorking (anteprima bozza): la versione di lavorazione più recente (bozza inclusa), non la pubblicata.
        int? versionId;
        if (preferWorking)
            versionId = await _db.DocumentVersions
                .Where(v => v.DocumentId == doc.Id)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => (int?)v.Id)
                .FirstOrDefaultAsync(ct);
        else
            versionId = doc.CurrentVersionId
                ?? await _db.DocumentVersions
                    .Where(v => v.DocumentId == doc.Id && v.Status == DocumentStatus.Published)
                    .OrderByDescending(v => v.VersionNumber)
                    .Select(v => (int?)v.Id)
                    .FirstOrDefaultAsync(ct);
        if (versionId is null) return null;

        var sections = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == versionId)
            .AsNoTracking().ToListAsync(ct);

        var blocks = await _db.ContentBlocks
            .Where(b => b.DocumentVersionId == versionId)
            .Include(b => b.ScopeSector)
            .AsNoTracking().ToListAsync(ct);

        var blocksBySection = blocks
            .GroupBy(b => b.SectionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Order).ToList());

        var childrenByParent = sections
            .Where(s => s.ParentSectionId != null)
            .GroupBy(s => s.ParentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Order).ToList());

        RawSection Build(DocumentSection s) => new()
        {
            Id = s.Id,
            Title = s.Title,
            Depth = s.Depth,
            SectionKey = s.SectionKey,
            Order = s.Order,
            RenderMode = s.RenderMode,
            Blocks = (blocksBySection.TryGetValue(s.Id, out var bs) ? bs : new())
                .Select(MapBlock).ToList(),
            Children = (childrenByParent.TryGetValue(s.Id, out var cs) ? cs : new())
                .Select(Build).ToList(),
        };

        var roots = sections
            .Where(s => s.ParentSectionId is null)
            .OrderBy(s => s.Order)
            .Select(Build).ToList();

        return new RawDocument
        {
            Title = doc.Title,
            AiracCycle = doc.LastUpdatedAiracCycle,
            Roots = roots,
        };
    }

    private static RawBlock MapBlock(ContentBlock b) => new()
    {
        Id = b.Id,
        Order = b.Order,
        Format = b.Format,
        Visibility = b.Visibility,
        Tier = b.Tier,
        ScopeSectorKey = b.ScopeSector?.Callsign,
        Body = b.Body,
        BodyJson = b.BodyJson,
        CalloutKind = b.CalloutKind,
    };

    /// <summary>Determina (tipo, chiave) di release per un documento: vLOA → docId; vIPI d'aeroporto → ICAO.
    /// Gli altri (ACC vIPI legacy) non hanno release doc-based (null → path pubblicato storico).</summary>
    private async Task<(ReleaseTargetType?, string?)> ResolveReleaseTargetAsync(Document doc, CancellationToken ct)
    {
        if (doc.Type == DocumentType.Vloa)
            return (ReleaseTargetType.Vloa, doc.Id.ToString());

        // APP non remotizzato su Document (doc 08e): target release = callsign APP.
        var appCallsign = await _db.Sectors.AsNoTracking()
            .Where(s => s.DocumentId == doc.Id && s.IsPrimary && s.Type == SectorType.App
                        && s.ApproachKind == ApproachKind.Standalone)
            .Select(s => s.Callsign).FirstOrDefaultAsync(ct);
        if (appCallsign is not null) return (ReleaseTargetType.App, appCallsign);

        var icao = await _db.Sectors.AsNoTracking()
            .Where(s => s.DocumentId == doc.Id && s.Kind == SectorKind.Airport && s.AirportIcao != null)
            .Select(s => s.AirportIcao).FirstOrDefaultAsync(ct);
        return icao is not null ? (ReleaseTargetType.Airport, icao) : (null, null);
    }

    /// <summary>Costruisce un <see cref="RawDocument"/> dall'albero sezioni/blocchi di una versione. Riusato dal
    /// viewer (versione pubblicata) e dallo snapshot delle release (versione working). AiracCycle passato dal chiamante.</summary>
    public static async Task<RawDocument?> BuildRawFromVersionAsync(
        VipiDbContext db, int versionId, string title, string airacCycle, CancellationToken ct)
    {
        var sections = await db.DocumentSections
            .Where(s => s.DocumentVersionId == versionId).AsNoTracking().ToListAsync(ct);
        if (sections.Count == 0) return null;

        var blocks = await db.ContentBlocks
            .Where(b => b.DocumentVersionId == versionId).Include(b => b.ScopeSector)
            .AsNoTracking().ToListAsync(ct);

        var blocksBySection = blocks.GroupBy(b => b.SectionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Order).ToList());
        var childrenByParent = sections.Where(s => s.ParentSectionId != null)
            .GroupBy(s => s.ParentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Order).ToList());

        RawSection Build(DocumentSection s) => new()
        {
            Id = s.Id, Title = s.Title, Depth = s.Depth, SectionKey = s.SectionKey, Order = s.Order,
            RenderMode = s.RenderMode,
            Blocks = (blocksBySection.TryGetValue(s.Id, out var bs) ? bs : new()).Select(MapBlock).ToList(),
            Children = (childrenByParent.TryGetValue(s.Id, out var cs) ? cs : new()).Select(Build).ToList(),
        };

        var roots = sections.Where(s => s.ParentSectionId is null).OrderBy(s => s.Order).Select(Build).ToList();
        return new RawDocument { Title = title, AiracCycle = airacCycle, Roots = roots };
    }
}
