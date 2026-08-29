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

    public Task<RawDocument?> LoadAirportVipiAsync(string icao, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default)
    {
        // ignoreRelease/preferWorking (anteprima bozza, gated all'editor): mostra anche i documenti/aeroporti nascosti
        // dall'admin e la versione di lavorazione più recente, anche se il documento non è ancora stato pubblicato.
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vipi
                 && (preferWorking || ignoreRelease || !d.IsHidden)
                 // Dall'AEROPORTO: è lui a dire qual è il suo documento. Prima si passava dai settori con la
                 // regola SectorDocumentRules, che doveva escludere a mano l'APP non remotizzato — anch'esso
                 // Kind=Airport con questo ICAO, ma con un documento tutto suo. Ora la domanda non si pone.
                 && _db.Airports.Any(a => a.Icao == icao && a.DocumentId == d.Id)
                 // Aeroporto nascosto dall'admin ⇒ pagina pubblica inaccessibile (ma visibile in anteprima bozza).
                 && (preferWorking || ignoreRelease || !_db.Airports.Any(a => a.Icao == icao && a.IsHidden)),
            ignoreRelease, preferWorking, ct);
    }

    public Task<RawDocument?> LoadAirportMilVipiAsync(string icao, bool ignoreRelease = false, bool preferWorking = false, CancellationToken ct = default)
    {
        // Gemella di LoadAirportVipiAsync, e passa dal legame MILITARE. ⚠️ Il filtro sull'edizione non è
        // ridondante col legame: se un giorno un documento civile finisse per errore in MilDocumentId,
        // questa query lo scarterebbe invece di mostrarlo come vSOP militare.
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vipi
                 && d.Edition == DocumentEdition.Military
                 && (preferWorking || ignoreRelease || !d.IsHidden)
                 && _db.Airports.Any(a => a.Icao == icao && a.MilDocumentId == d.Id)
                 && (preferWorking || ignoreRelease || !_db.Airports.Any(a => a.Icao == icao && a.IsHidden)),
            ignoreRelease, preferWorking, ct);
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
            IsHidden = s.IsHidden,
            BeforeParentBody = s.BeforeParentBody, Audience = s.Audience,
            LeadSentence = s.LeadSentence,
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
            Language = doc.Language,
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
    /// <summary>
    /// Di quale release è il bersaglio questo documento. ⚠️ È una <b>quinta</b> risoluzione scritta a mano —
    /// i descrittori <c>IReleaseTarget</c> decidono guardando le NAVIGAZIONI, e qui il documento arriva da una
    /// query senza <c>Include</c> — quindi si passa dalle colonne, e per farlo bisogna sapere che le colonne
    /// sono <b>due</b>.
    ///
    /// <para>
    /// ⚠️ <b>L'edizione si guarda PRIMA di tutto il resto</b> (correzione del 29 agosto 2026). Un documento
    /// militare non è agganciato a <c>Sector.DocumentId</c> né a <c>Airport.DocumentId</c> — quei legami sono
    /// del gemello civile — quindi le due domande di sotto rispondevano <c>null</c> tutte e due, il bersaglio
    /// restava sconosciuto, lo snapshot della release non veniva nemmeno cercato e il chiamante concludeva
    /// «nessuna release effettiva». Effetto a schermo: <b>un vSOP militare pubblicato e in vigore mostrava
    /// «Nessun vSOP militare pubblicato»</b>. Non si era visto perché l'unico documento militare guardato a
    /// schermo era in BOZZA, e la bozza prende un'altra strada (<c>ignoreRelease</c>).
    /// </para>
    /// </summary>
    private async Task<(ReleaseTargetType?, string?)> ResolveReleaseTargetAsync(Document doc, CancellationToken ct)
    {
        if (doc.Type == DocumentType.Vloa)
            return (ReleaseTargetType.Vloa, doc.Id.ToString());

        if (doc.Edition == DocumentEdition.Military)
        {
            // Gli stessi due bersagli del civile, letti dalle colonne gemelle. La CHIAVE è la stessa —
            // l'ICAO, il callsign — perché a distinguere le due edizioni è il TIPO.
            var milApp = await _db.Sectors.AsNoTracking()
                .Where(s => s.MilDocumentId == doc.Id && s.IsPrimary && s.Type == SectorType.App
                            && s.ApproachKind == ApproachKind.Standalone)
                .Select(s => s.Callsign).FirstOrDefaultAsync(ct);
            if (milApp is not null) return (ReleaseTargetType.AppMil, milApp);

            var milIcao = await _db.Airports.AsNoTracking()
                .Where(a => a.MilDocumentId == doc.Id).Select(a => a.Icao).FirstOrDefaultAsync(ct);
            return milIcao is not null ? (ReleaseTargetType.AirportMil, milIcao) : (null, null);
        }

        // APP non remotizzato su Document (doc 08e): target release = callsign APP.
        var appCallsign = await _db.Sectors.AsNoTracking()
            .Where(s => s.DocumentId == doc.Id && s.IsPrimary && s.Type == SectorType.App
                        && s.ApproachKind == ApproachKind.Standalone)
            .Select(s => s.Callsign).FirstOrDefaultAsync(ct);
        if (appCallsign is not null) return (ReleaseTargetType.App, appCallsign);

        var icao = await _db.Airports.AsNoTracking()
            .Where(a => a.DocumentId == doc.Id).Select(a => a.Icao).FirstOrDefaultAsync(ct);
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
            RenderMode = s.RenderMode, IsHidden = s.IsHidden, BeforeParentBody = s.BeforeParentBody, Audience = s.Audience,
        LeadSentence = s.LeadSentence,
            Blocks = (blocksBySection.TryGetValue(s.Id, out var bs) ? bs : new()).Select(MapBlock).ToList(),
            Children = (childrenByParent.TryGetValue(s.Id, out var cs) ? cs : new()).Select(Build).ToList(),
        };

        var roots = sections.Where(s => s.ParentSectionId is null).OrderBy(s => s.Order).Select(Build).ToList();
        return new RawDocument { Title = title, AiracCycle = airacCycle, Roots = roots };
    }
}
