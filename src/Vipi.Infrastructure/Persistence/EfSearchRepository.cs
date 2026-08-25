using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Ricerca full-text sulle versioni pubblicate correnti. Match case-insensitive in memoria
/// (scala pilota) su titolo documento, titoli sezione e corpo blocchi (Body + BodyJson).
/// </summary>
public sealed class EfSearchRepository : ISearchRepository
{
    private readonly VipiDbContext _db;
    private readonly IReleaseTargetRegistry _targets;
    private readonly IDocRoutesRegistry _routes;
    private readonly IReleaseRepository _releases;

    public EfSearchRepository(VipiDbContext db, IReleaseTargetRegistry targets, IDocRoutesRegistry routes,
        IReleaseRepository releases)
    {
        _db = db;
        _targets = targets;
        _routes = routes;
        _releases = releases;
    }

    private sealed record DocMeta(int DocId, int VersionId, string Title, DocumentType Type, string Url);

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope, int limit, CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Where(d => d.CurrentVersionId != null)
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            // L'aeroporto descritto: da qui il descrittore prende ICAO e ACC (vedi AirportReleaseTarget).
            .Include(d => d.Airport).ThenInclude(a => a!.Acc)
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
            .AsNoTracking().ToListAsync(ct);

        // Tipo, ACC e ROTTA di ogni documento vengono dai descrittori (doc 13 §3e): la stessa attribuzione
        // dell'elenco unificato, e le URL dal registry delle rotte. Qui ce n'era una copia scritta a mano che
        // distingueva solo «aeroporto» da «tutto il resto» → OGNI documento di APP standalone finiva su
        // /services/vsop/{acc}/vipi, cioè sulla vIPI di ACC, invece che sulla propria pagina.
        var described = docs
            .Select(d => (Doc: d, Managed: Describe(d)))
            .Where(x => x.Managed is not null && InScope(scope, x.Managed!.Kind) && !string.IsNullOrEmpty(x.Managed!.AccCode))
            .ToList();

        // Un indice non è un posto meno pubblico della pagina (doc 13 §3f): stesso gate — non nascosto e con
        // release AIRAC effettiva. Prima bastava avere una versione corrente, e uscivano documenti nascosti
        // dall'admin e contenuto di versioni che nessuna pagina serve.
        var visible = await PublicDocumentGate.VisibleAsync(
            described, x => x.Doc, x => x.Managed!, _releases, ct);

        var metas = new List<DocMeta>();
        foreach (var (d, managed) in visible)
        {
            var url = _routes.For(managed!.Kind).PublicUrl(
                managed.AccCode!.ToLowerInvariant(), managed.ReleaseKey, managed.NeighbourCode);
            if (url is null) continue;
            metas.Add(new DocMeta(d.Id, d.CurrentVersionId!.Value, d.Title, d.Type, url));
        }

        var versionIds = metas.Select(m => m.VersionId).ToList();

        // Le SEZIONI si leggono tutte: servono intere sia per il percorso «padre › figlio» sia per calcolare i
        // sottoalberi nascosti, e sono righe piccole (titolo e poco altro).
        var sections = await _db.DocumentSections.Where(s => versionIds.Contains(s.DocumentVersionId))
            .AsNoTracking().ToListAsync(ct);

        // I BLOCCHI no. Qui c'era la stessa lettura senza filtro, e i blocchi sono le righe grosse del
        // database: Body e soprattutto BodyJson portano i poligoni AoR, le tabelle di configurazione e gli
        // envelope delle immagini. Ogni ricerca — su una pagina pubblica e anonima, senza limitatore —
        // trasferiva e allocava l'intero contenuto pubblicato, e poi buttava via quasi tutto in memoria.
        //
        // Il filtro va nel database. `ToLower().Contains(...)` diventa LOWER(col) LIKE '%…%' su tutti e tre i
        // provider, ed è insensibile alle maiuscole **indipendentemente dalla collation** — che è quel che
        // serve, perché su MariaDB la collation è `as_cs` e un LIKE nudo cambierebbe semantica in silenzio.
        // Le maiuscole restano indifferenti e gli accenti restano significativi, esattamente come faceva
        // OrdinalIgnoreCase in memoria.
        //
        // ⚠️ I blocchi IMMAGINE entrano comunque, senza filtro: il loro testo cercabile non è nella colonna
        // (è l'alternativo + didascalia estratti dall'envelope) e nessun WHERE può guardarlo. Sono pochi.
        var q = query.ToLowerInvariant();
        var blocks = await _db.ContentBlocks
            .Where(b => versionIds.Contains(b.DocumentVersionId))
            .Where(b => b.Format == BlockFormat.Image
                        || (b.Body != null && b.Body.ToLower().Contains(q))
                        || (b.BodyJson != null && b.BodyJson.ToLower().Contains(q)))
            .AsNoTracking().ToListAsync(ct);

        var secById = sections.ToDictionary(s => s.Id);
        // Sezioni nascoste (e i loro sottoalberi): fuori dall'indice, come sono fuori dalla pagina.
        var hiddenSections = PublicDocumentGate.HiddenSectionIds(sections);

        // Raggruppati una volta sola. Prima erano due `Where` dentro il ciclo sui documenti, cioè una
        // riscansione completa delle liste per ogni documento: O(documenti × sezioni) e O(documenti × blocchi).
        var sectionsByVersion = sections.ToLookup(s => s.DocumentVersionId);
        var blocksByVersion = blocks.ToLookup(b => b.DocumentVersionId);

        var hits = new List<SearchHit>();

        bool Has(string? text) => !string.IsNullOrEmpty(text) && text.Contains(query, StringComparison.OrdinalIgnoreCase);

        foreach (var m in metas)
        {
            if (hits.Count >= limit) break;
            var url = m.Url;

            // 1) titolo documento
            if (Has(m.Title))
                hits.Add(new SearchHit { DocTitle = m.Title, DocType = m.Type, Where = m.Title, Snippet = m.Title, Url = url });

            // 2) titoli sezione
            foreach (var s in sectionsByVersion[m.VersionId])
            {
                if (hits.Count >= limit) break;
                if (hiddenSections.Contains(s.Id)) continue;
                if (Has(s.Title))
                    hits.Add(Hit(m, secById, s.Id, s.Title, $"{url}#s-{s.Id}"));
            }

            // 3) corpo blocchi (Body + BodyJson)
            foreach (var b in blocksByVersion[m.VersionId])
            {
                if (hits.Count >= limit) break;
                if (hiddenSections.Contains(b.SectionId)) continue;
                // Un blocco immagine ha per testo il suo alternativo e la didascalia: il BodyJson porta lo sha, e
                // cercare "abc" non deve pescare un'immagine il cui sha contiene "abc" né mostrare JSON nel risultato.
                var searchable = b.Format == BlockFormat.Image
                    ? MediaRef.TextOf(b.BodyJson, b.Body)
                    : Has(b.Body) ? b.Body : Has(b.BodyJson) ? b.BodyJson : null;
                if (Has(searchable))
                    hits.Add(Hit(m, secById, b.SectionId, Snippet(searchable!, query), $"{url}#s-{b.SectionId}"));
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

    /// <summary>Attribuisce il documento a un tipo con gli stessi descrittori dell'elenco unificato.</summary>
    private ManagedDoc? Describe(Domain.Entities.Document doc)
    {
        foreach (var target in _targets.ByDescribeOrder)
            if (target.TryDescribe(doc, hasDraft: false, out var managed))
                return managed;
        return null;
    }

    private static bool InScope(SearchScope scope, ManagedDocKind kind) => scope switch
    {
        SearchScope.All => true,
        SearchScope.Vipi => kind == ManagedDocKind.AccVipi,
        SearchScope.App => kind == ManagedDocKind.AppVipi,
        SearchScope.Airport => kind == ManagedDocKind.AirportVipi,
        SearchScope.Vloa => kind == ManagedDocKind.Vloa,
        _ => true,
    };
}
