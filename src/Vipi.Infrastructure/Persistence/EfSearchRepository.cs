using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Ricerca full-text sulle versioni pubblicate correnti. Match case-insensitive in memoria
/// (scala pilota) su titolo documento, titoli sezione e corpo blocchi (Body + BodyJson).
/// </summary>
public sealed class EfSearchRepository : ISearchRepository
{
    private readonly VipiDbContext _db;
    public EfSearchRepository(VipiDbContext db) => _db = db;

    private sealed record DocMeta(int DocId, int VersionId, string Title, DocumentType Type, string AccCode, string UrlBase);

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope, int limit, CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Where(d => d.CurrentVersionId != null)
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            .AsNoTracking().ToListAsync(ct);

        // Risolve i metadati (ACC + rotta) e applica il filtro di scope.
        var metas = new List<DocMeta>();
        foreach (var d in docs)
        {
            string? acc; string urlBase;
            if (d.Type == DocumentType.Vloa)
            {
                acc = await _db.DocumentParties
                    .Where(pa => pa.DocumentId == d.Id && pa.Role == PartyRole.Home)
                    .Select(pa => pa.Sector!.Acc!.Code).FirstOrDefaultAsync(ct);
                urlBase = $"vloa/{d.Id}";
                if (scope is not (SearchScope.All or SearchScope.Vloa)) continue;
            }
            else
            {
                var primary = d.Sectors.FirstOrDefault(x => x.IsPrimary) ?? d.Sectors.FirstOrDefault();
                acc = primary?.Acc?.Code;
                var isAirport = primary?.Kind == SectorKind.Airport;
                urlBase = isAirport
                    ? (primary?.AirportIcao is string ic ? $"airports?icao={ic}" : "airports")
                    : "vipi";
                if (scope == SearchScope.Vipi && isAirport) continue;
                if (scope == SearchScope.Airport && !isAirport) continue;
                if (scope == SearchScope.Vloa) continue;
            }
            if (acc is null) continue;
            metas.Add(new DocMeta(d.Id, d.CurrentVersionId!.Value, d.Title, d.Type, acc, urlBase));
        }

        var versionIds = metas.Select(m => m.VersionId).ToList();
        var sections = await _db.DocumentSections.Where(s => versionIds.Contains(s.DocumentVersionId))
            .AsNoTracking().ToListAsync(ct);
        var blocks = await _db.ContentBlocks.Where(b => versionIds.Contains(b.DocumentVersionId))
            .AsNoTracking().ToListAsync(ct);

        var secById = sections.ToDictionary(s => s.Id);
        var hits = new List<SearchHit>();

        bool Has(string? text) => !string.IsNullOrEmpty(text) && text.Contains(query, StringComparison.OrdinalIgnoreCase);

        foreach (var m in metas)
        {
            if (hits.Count >= limit) break;
            var url = $"/vsop/{m.AccCode.ToLowerInvariant()}/{m.UrlBase}";

            // 1) titolo documento
            if (Has(m.Title))
                hits.Add(new SearchHit { DocTitle = m.Title, DocType = m.Type, Where = m.Title, Snippet = m.Title, Url = url });

            // 2) titoli sezione
            foreach (var s in sections.Where(s => s.DocumentVersionId == m.VersionId))
            {
                if (hits.Count >= limit) break;
                if (Has(s.Title))
                    hits.Add(Hit(m, secById, s.Id, s.Title, $"{url}#s-{s.Id}"));
            }

            // 3) corpo blocchi (Body + BodyJson)
            foreach (var b in blocks.Where(b => b.DocumentVersionId == m.VersionId))
            {
                if (hits.Count >= limit) break;
                var text = Has(b.Body) ? b.Body : Has(b.BodyJson) ? b.BodyJson : null;
                if (text is not null)
                    hits.Add(Hit(m, secById, b.SectionId, Snippet(text!, query), $"{url}#s-{b.SectionId}"));
            }
        }

        return hits;
    }

    private SearchHit Hit(DocMeta m, IReadOnlyDictionary<int, Domain.Entities.DocumentSection> secById, int sectionId, string snippet, string url) =>
        new()
        {
            DocTitle = m.Title,
            DocType = m.Type,
            Where = $"{m.Title} › {SectionPath(secById, sectionId)}",
            Snippet = snippet,
            Url = url,
        };

    /// <summary>Percorso "Sezione padre › Sezione" risalendo i genitori.</summary>
    private static string SectionPath(IReadOnlyDictionary<int, Domain.Entities.DocumentSection> secById, int sectionId)
    {
        var parts = new List<string>();
        int? cur = sectionId;
        var guard = 0;
        while (cur is int id && secById.TryGetValue(id, out var s) && guard++ < 5)
        {
            parts.Insert(0, s.Title);
            cur = s.ParentSectionId;
        }
        return string.Join(" › ", parts);
    }

    /// <summary>Finestra di ~120 char attorno al primo match, con ellissi.</summary>
    private static string Snippet(string text, string query)
    {
        var idx = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return text.Length <= 160 ? text : text[..160] + "…";
        var start = Math.Max(0, idx - 50);
        var end = Math.Min(text.Length, idx + query.Length + 70);
        var s = text[start..end];
        if (start > 0) s = "…" + s;
        if (end < text.Length) s += "…";
        return s;
    }
}
