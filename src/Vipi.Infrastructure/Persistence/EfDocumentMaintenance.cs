using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IDocumentMaintenance"/>
public sealed class EfDocumentMaintenance : IDocumentMaintenance
{
    private const string HiddenSectionsProperty = "HiddenSections";
    private const string MinimaKey = "minima";
    private const string PurposeKey = "purpose";
    private const string PurposeTitle = "Purpose";

    private readonly VipiDbContext _db;

    public EfDocumentMaintenance(VipiDbContext db) => _db = db;

    public async Task<int> ReconcileCustomSectionKeysAsync(CancellationToken ct = default)
    {
        // Solo le sezioni con la chiave storica ambigua: le nuove nascono già univoche (doc 11 §3a).
        var legacy = await _db.DocumentSections
            .Where(s => s.SectionKey == SectionKeys.LegacyCustom)
            .ToListAsync(ct);
        if (legacy.Count == 0) return 0;

        foreach (var s in legacy)
        {
            s.SectionKey = SectionKeys.NewCustom();
            s.RowVersion = Guid.NewGuid().ToByteArray();
        }
        await _db.SaveChangesAsync(ct);
        return legacy.Count;
    }

    public async Task<int> MigrateHiddenSectionsAsync(CancellationToken ct = default)
    {
        var touched = await FromDocumentProfilesAsync(ct);
        touched += await FromAccBlockMetaAsync(ct);
        return touched;
    }

    public async Task<int> ReconcileVloaSectionKeysAsync(CancellationToken ct = default)
    {
        var touched = await ReconcileCoordinationDirectionsAsync(ct);
        touched += await ReconcilePurposeKeyAsync(ct);
        if (touched > 0) await _db.SaveChangesAsync(ct);
        return touched;
    }

    /// <summary>Le due figlie di «coordination» prendono una chiave per direzione, nell'ordine in cui il registro
    /// le semina (prima Home→vicino). Idempotente: cerca solo le figlie che ripetono ancora la chiave del padre.</summary>
    private async Task<int> ReconcileCoordinationDirectionsAsync(CancellationToken ct)
    {
        var parentIds = await _db.DocumentSections
            .Where(s => s.SectionKey == SectionKeys.Coordination && s.ParentSectionId == null)
            .Select(s => s.Id).ToListAsync(ct);
        if (parentIds.Count == 0) return 0;

        var children = await _db.DocumentSections
            .Where(s => s.ParentSectionId != null && parentIds.Contains(s.ParentSectionId!.Value)
                        && s.SectionKey == SectionKeys.Coordination)
            .ToListAsync(ct);
        if (children.Count == 0) return 0;

        var touched = 0;
        foreach (var group in children.GroupBy(s => s.ParentSectionId!.Value))
        {
            var ordered = group.OrderBy(s => s.Order).ThenBy(s => s.Id).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                // Oltre le due direzioni non c'è nulla da riconciliare: una terza figlia con la chiave del padre
                // non è una direzione, e inventarle un verso sarebbe peggio che lasciarla libera.
                if (i > 1) break;
                ordered[i].SectionKey = i == 0 ? SectionKeys.CoordinationOut : SectionKeys.CoordinationIn;
                ordered[i].RowVersion = Guid.NewGuid().ToByteArray();
                touched++;
            }
        }

        // I blocchi delle direzioni non li rende nessuno (il corpo lo produce la pagina): erano i due paragrafi
        // seminati dal registro, scritti nel DB di ogni vLOA e invisibili ovunque.
        var directionIds = children.Select(s => s.Id).ToList();
        var orphanBlocks = await _db.ContentBlocks.Where(b => directionIds.Contains(b.SectionId)).ToListAsync(ct);
        if (orphanBlocks.Count > 0) _db.ContentBlocks.RemoveRange(orphanBlocks);

        return touched;
    }

    /// <summary>«Purpose» nasceva con una chiave libera perché il catalogo non la conosceva: la si riconosce per
    /// titolo — è l'unico appiglio rimasto, ed è legittimo in una riconciliazione one-shot — e solo dentro le vLOA.</summary>
    private async Task<int> ReconcilePurposeKeyAsync(CancellationToken ct)
    {
        var candidates = await _db.DocumentSections
            .Where(s => s.ParentSectionId == null
                        && s.Title == PurposeTitle
                        && s.DocumentVersion!.Document!.Type == Vipi.Domain.DocumentType.Vloa)
            .ToListAsync(ct);

        var touched = 0;
        foreach (var s in candidates)
        {
            if (!SectionKeys.IsCustom(s.SectionKey)) continue;   // già riconciliata
            s.SectionKey = PurposeKey;
            s.RowVersion = Guid.NewGuid().ToByteArray();
            touched++;
        }
        return touched;
    }

    public async Task<int> ClearMinimaPlaceholderBlocksAsync(CancellationToken ct = default)
    {
        // Solo i placeholder: blocco senza testo E senza JSON. Un blocco con contenuto è roba di un editore e resta.
        var stale = await _db.ContentBlocks
            .Where(b => b.Section!.SectionKey == MinimaKey
                        && (b.Body == null || b.Body == "")
                        && (b.BodyJson == null || b.BodyJson == ""))
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;

        _db.ContentBlocks.RemoveRange(stale);
        await _db.SaveChangesAsync(ct);
        return stale.Count;
    }

    /// <summary>APP (chiavi) e vLOA (titoli): un'unica colonna <c>DocumentProfiles.HiddenSectionsJson</c>, per
    /// documento e non versionata. Si marcano le sezioni di TUTTE le versioni del documento (l'intento «nascosta» è
    /// del documento, non di una singola bozza), poi si azzera la sorgente: senza, rieseguire la migrazione
    /// rimetterebbe nascosto ciò che nel frattempo l'editore ha rimesso pubblico.</summary>
    private async Task<int> FromDocumentProfilesAsync(CancellationToken ct)
    {
        var profiles = await _db.DocumentProfiles
            .Where(p => p.HiddenSectionsJson != null && p.HiddenSectionsJson != "")
            .ToListAsync(ct);
        if (profiles.Count == 0) return 0;

        var touched = 0;
        foreach (var profile in profiles)
        {
            var hidden = ParseStrings(profile.HiddenSectionsJson);
            profile.HiddenSectionsJson = null;
            if (hidden.Count == 0) continue;

            var sections = await _db.DocumentSections
                .Where(s => s.DocumentVersion!.DocumentId == profile.DocumentId)
                .ToListAsync(ct);
            touched += MarkHidden(sections, hidden);
        }
        await _db.SaveChangesAsync(ct);
        return touched;
    }

    /// <summary>vIPI ACC: le chiavi nascoste stavano nel blockmeta (BodyJson del blocco proprio della sezione-blocco)
    /// e valgono per le sezioni figlie di QUEL blocco. Il JSON viene riscritto senza la proprietà, così la migrazione
    /// resta idempotente.</summary>
    private async Task<int> FromAccBlockMetaAsync(CancellationToken ct)
    {
        var candidates = await _db.ContentBlocks
            .Include(b => b.Section)
            .Where(b => b.BodyJson != null && b.BodyJson.Contains(HiddenSectionsProperty)
                        && b.Section!.ParentSectionId == null)
            .ToListAsync(ct);
        if (candidates.Count == 0) return 0;

        var touched = 0;
        foreach (var block in candidates)
        {
            if (ParseObject(block.BodyJson) is not { } meta) continue;
            if (meta[HiddenSectionsProperty] is not JsonArray array) continue;

            var hidden = array.Select(n => n?.GetValue<string>()).OfType<string>().ToList();
            meta.Remove(HiddenSectionsProperty);
            block.BodyJson = meta.ToJsonString();
            block.RowVersion = Guid.NewGuid().ToByteArray();
            if (hidden.Count == 0) continue;

            var children = await _db.DocumentSections
                .Where(s => s.ParentSectionId == block.SectionId)
                .ToListAsync(ct);
            touched += MarkHidden(children, hidden);
        }
        await _db.SaveChangesAsync(ct);
        return touched;
    }

    /// <summary>Marca le sezioni identificate dalle voci storiche: per chiave (ACC/APP) o per titolo (vLOA). La voce
    /// ambigua <c>custom</c> non identifica più nulla dopo la riconciliazione delle chiavi, quindi si espande a TUTTE
    /// le sezioni libere: conservativo, non scopre in pubblico ciò che l'editore riteneva nascosto.</summary>
    private static int MarkHidden(IReadOnlyList<DocumentSection> sections, IReadOnlyList<string> hidden)
    {
        var byKeyOrTitle = new HashSet<string>(hidden, StringComparer.OrdinalIgnoreCase);
        var allCustom = byKeyOrTitle.Contains(SectionKeys.LegacyCustom);

        var touched = 0;
        foreach (var s in sections)
        {
            if (s.IsHidden) continue;
            var match = byKeyOrTitle.Contains(s.SectionKey)
                        || byKeyOrTitle.Contains(s.Title)
                        || (allCustom && SectionKeys.IsCustom(s.SectionKey));
            if (!match) continue;
            s.IsHidden = true;
            s.RowVersion = Guid.NewGuid().ToByteArray();
            touched++;
        }
        return touched;
    }

    private static List<string> ParseStrings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }

    private static JsonObject? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
    }
}
