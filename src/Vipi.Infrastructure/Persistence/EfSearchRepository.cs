using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Application.Routing;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Ricerca full-text su ciò che il pubblico vede: lo snapshot della release in vigore di ogni documento
/// pubblico. Match case-insensitive in memoria su titolo documento, titoli sezione e corpo blocchi
/// (Body + BodyJson), sull'indice tenuto da <see cref="IndiceDelleRelease"/>.
/// </summary>
public sealed class EfSearchRepository : ISearchRepository
{
    private readonly VipiDbContext _db;
    private readonly IReleaseTargetRegistry _targets;
    private readonly IDocRoutesRegistry _routes;
    private readonly IReleaseRepository _releases;
    private readonly IndiceDelleRelease _indice;

    /// <param name="indice">Singleton in produzione. Null = un indice proprio (test che costruiscono il
    /// repository a mano su database che si ripetono gli id).</param>
    public EfSearchRepository(VipiDbContext db, IReleaseTargetRegistry targets, IDocRoutesRegistry routes,
        IReleaseRepository releases, IndiceDelleRelease? indice = null)
    {
        _db = db;
        _targets = targets;
        _routes = routes;
        _releases = releases;
        _indice = indice ?? new IndiceDelleRelease();
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope, int limit, CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Where(d => d.CurrentVersionId != null)
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            // L'aeroporto descritto: da qui il descrittore prende ICAO e ACC (vedi AirportReleaseTarget).
            .Include(d => d.Airport).ThenInclude(a => a!.Acc)
            // ⚠️ E quello dell'edizione MILITARE: legame diverso, navigazione diversa. Senza, il documento
            // militare non viene descritto da nessuno e sparisce di qui in silenzio — la spiegazione lunga
            // sta su `EfDocumentAdminRepository.ListAsync`, che fa la stessa query.
            .Include(d => d.MilAirport).ThenInclude(a => a!.Acc)
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

        // 🔴 E stesso CONTENUTO (T-042, 13 settembre 2026): la pagina serve lo snapshot della release in vigore,
        // e l'indice legge quello — non la versione corrente, che dopo un «Pubblica questa versione» con la
        // release al ciclo successivo è già un'altra. Le sezioni nascoste sono quelle congelate nello snapshot.
        //
        // ⚠️ Il costo resta basso per costruzione: una query per le teste delle release (senza payload), e i
        // payload solo per le release che l'indice non ha ancora visto — una volta per pubblicazione, non una
        // volta per tasto. È la stessa preoccupazione dell'11 agosto 2026, quando ogni ricerca leggeva l'intero
        // contenuto pubblicato.
        var teste = await ReleaseInVigore.TesteAsync(_db,
            visible.Select(v => (v.Managed!.ReleaseTarget, v.Managed!.ReleaseKey)).Distinct().ToList(),
            DateTime.UtcNow, ct);
        // Le voci delle release uscite di vigore si buttano solo quando la ricerca guarda TUTTO: una ricerca
        // filtrata per tipo vede una parte dei bersagli, e buttare il resto vorrebbe dire rileggerlo subito dopo.
        if (scope == SearchScope.All) _indice.TieniSolo(teste.Values);

        var hits = new List<SearchHit>();
        bool Has(string? text) => !string.IsNullOrEmpty(text) && text.Contains(query, StringComparison.OrdinalIgnoreCase);

        foreach (var (doc, managed) in visible)
        {
            if (hits.Count >= limit) break;
            if (!teste.TryGetValue((managed!.ReleaseTarget, managed.ReleaseKey), out var testa)) continue;
            var url = _routes.For(managed.Kind).PublicUrl(
                managed.AccCode!.ToLowerInvariant(), managed.ReleaseKey, managed.NeighbourCode);
            if (url is null) continue;
            if (await _indice.VoceAsync(_db, testa, ct) is not { } voce) continue;

            // 1) titolo documento
            if (Has(voce.Titolo))
                hits.Add(new SearchHit { DocTitle = voce.Titolo, DocType = doc.Type, Where = voce.Titolo, Snippet = voce.Titolo, Url = url });

            // 2) titoli sezione
            foreach (var s in voce.Sezioni)
            {
                if (hits.Count >= limit) break;
                if (Has(s.Titolo))
                    hits.Add(Hit(voce.Titolo, doc.Type, s, s.Titolo, url));
            }

            // 3) corpo dei blocchi: un risultato per blocco, col primo dei suoi testi che combacia
            foreach (var s in voce.Sezioni)
            {
                if (hits.Count >= limit) break;
                foreach (var (primo, secondo) in s.Testi)
                {
                    if (hits.Count >= limit) break;
                    var testo = Has(primo) ? primo : Has(secondo) ? secondo : null;
                    if (testo is not null)
                        hits.Add(Hit(voce.Titolo, doc.Type, s, Snippet(testo, query), url));
                }
            }
        }

        return hits;
    }

    private static SearchHit Hit(string docTitle, DocumentType tipo, IndiceDelleRelease.Sezione s, string snippet, string url) =>
        new()
        {
            DocTitle = docTitle,
            DocType = tipo,
            Where = $"{docTitle} › {s.Percorso}",
            Snippet = snippet,
            Url = $"{url}#s-{s.Id}",
        };

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

    private static bool InScope(SearchScope scope, ReleaseTargetType kind) => scope switch
    {
        SearchScope.All => true,
        SearchScope.Vipi => kind == ReleaseTargetType.AccVipi,
        SearchScope.App => kind == ReleaseTargetType.App,
        SearchScope.Airport => kind == ReleaseTargetType.Airport,
        SearchScope.Vloa => kind == ReleaseTargetType.Vloa,
        SearchScope.Mil => kind is ReleaseTargetType.AirportMil or ReleaseTargetType.AppMil,
        _ => true,
    };
}
