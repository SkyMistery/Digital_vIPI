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
    private readonly IProcedureCercabili? _procedure;

    /// <param name="indice">Singleton in produzione. Null = un indice proprio (test che costruiscono il
    /// repository a mano su database che si ripetono gli id).</param>
    /// <param name="procedure">SID e STAR degli scali (1 ottobre 2026). Null = la ricerca non le guarda (test che non
    /// se ne curano).</param>
    public EfSearchRepository(VipiDbContext db, IReleaseTargetRegistry targets, IDocRoutesRegistry routes,
        IReleaseRepository releases, IndiceDelleRelease? indice = null, IProcedureCercabili? procedure = null)
    {
        _procedure = procedure;
        _db = db;
        _targets = targets;
        _routes = routes;
        _releases = releases;
        _indice = indice ?? new IndiceDelleRelease();
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, SearchScope scope, int limit, CancellationToken ct = default)
    {
        // ⚠️ Niente filtro su CurrentVersionId (U-057, revisione 3): il contenuto viene dallo snapshot della
        // release in vigore (T-042), e un documento in vigore senza «Pubblica versione» — la vLOA 65, uno
        // pubblicato solo con release programmata — ne resta senza. Il cancello sulla release basta.
        var docs = await _db.Documents
            .Include(d => d.Sectors).ThenInclude(s => s.Acc)
            // L'aeroporto descritto: da qui il descrittore prende ICAO e ACC (vedi AirportReleaseTarget).
            .Include(d => d.Airport).ThenInclude(a => a!.Acc)
            .Include(d => d.AtcUnit).ThenInclude(u => u!.Acc)
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

        // 🔴 In ordine di IMPORTANZA (committente, 30 settembre 2026): prima i documenti che hanno il termine nel
        // titolo, poi nel titolo di una sezione, poi di una sotto-sezione, poi nel testo. Prima uscivano documento
        // per documento, e il cinquantesimo risultato poteva essere un titolo mentre il primo era una riga di
        // tabella. Per questo si raccolgono TUTTI e si taglia dopo: tagliare prima vorrebbe dire ordinare i primi
        // cinquanta trovati, non i cinquanta più importanti. L'ordine fra pari resta quello dei documenti.
        var hits = new List<(int Peso, SearchHit Hit)>();
        bool Has(string? text) => !string.IsNullOrEmpty(text) && text.Contains(query, StringComparison.OrdinalIgnoreCase);

        foreach (var (doc, managed) in visible)
        {
            if (!teste.TryGetValue((managed!.ReleaseTarget, managed.ReleaseKey), out var testa)) continue;
            var url = _routes.For(managed.Kind).PublicUrl(
                managed.AccCode!.ToLowerInvariant(), managed.ReleaseKey, managed.NeighbourCode);
            if (url is null) continue;
            if (await _indice.VoceAsync(_db, testa, ct) is not { } voce) continue;

            // 0) titolo documento
            if (Has(voce.Titolo))
                hits.Add((PesoTitolo, new SearchHit { DocTitle = voce.Titolo, DocType = doc.Type, Where = voce.Titolo, Snippet = voce.Titolo, Url = url }));

            // 1–2) titoli di sezione e di sotto-sezione
            foreach (var s in voce.Sezioni)
                if (Has(s.Titolo))
                    hits.Add((s.Livello == 0 ? PesoSezione : PesoSottoSezione, Hit(voce.Titolo, doc.Type, s, s.Titolo, url)));

            // 2-bis) SID e STAR dello scalo (committente, 1 ottobre 2026: cercando ALAXI il documento di Napoli non
            //       usciva). Una riga per procedura, agganciata alla sua sezione: se la sezione è nascosta non è
            //       nell'indice, e la procedura non esce. Pesano come il testo: sono il contenuto della sezione.
            if (_procedure is not null && managed.Kind is ReleaseTargetType.Airport or ReleaseTargetType.AirportMil)
            {
                var sezSid = voce.Sezioni.FirstOrDefault(s => s.Chiave == "sids");
                var sezStar = voce.Sezioni.FirstOrDefault(s => s.Chiave == "stars");
                if (sezSid is not null || sezStar is not null)
                {
                    var p = await _procedure.PerScaloAsync(managed.ReleaseKey, managed.ReleaseTarget, ct);
                    if (sezSid is not null) Procedure(hits, voce.Titolo, doc.Type, sezSid, p.Sids, "SID", query, url);
                    if (sezStar is not null) Procedure(hits, voce.Titolo, doc.Type, sezStar, p.Stars, "STAR", query, url);
                }
            }

            // 3) corpo dei blocchi: un risultato per blocco, col primo dei suoi testi che combacia
            foreach (var s in voce.Sezioni)
                foreach (var (primo, secondo) in s.Testi)
                {
                    var testo = Has(primo) ? primo : Has(secondo) ? secondo : null;
                    if (testo is not null)
                        hits.Add((PesoTesto, Hit(voce.Titolo, doc.Type, s, Snippet(testo, query), url)));
                }
        }

        // OrderBy è stabile: fra risultati dello stesso peso resta l'ordine in cui sono stati trovati.
        return hits.OrderBy(h => h.Peso).Take(limit).Select(h => h.Hit).ToList();
    }

    private const int PesoTitolo = 0, PesoSezione = 1, PesoSottoSezione = 2, PesoTesto = 3;

    /// <summary>Una riga per procedura che combacia: codice, nome completo, piste, punti di partenza e transition.</summary>
    private static void Procedure(List<(int, SearchHit)> hits, string docTitle, DocumentType tipo, IndiceDelleRelease.Sezione s,
        IReadOnlyList<AirportSidRowView> righe, string verso, string query, string url)
    {
        foreach (var g in righe.Where(r => CercaProcedura.Combacia(r, query)).GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            static string Elenco(IEnumerable<string> v) =>
                string.Join(" ", v.Where(x => !string.IsNullOrWhiteSpace(x) && x != "—").Distinct(StringComparer.OrdinalIgnoreCase));
            var prima = g.First();
            var completo = CercaProcedura.NomeCompleto(prima);
            var nome = string.Equals(completo, prima.Name, StringComparison.OrdinalIgnoreCase) ? prima.Name : $"{prima.Name} ({completo})";
            var piste = Elenco(g.Select(r => r.Runway));
            var punti = Elenco(g.Select(r => r.Fix).Concat(g.Select(r => r.Transition)));
            var snippet = $"{verso} {nome}" + (piste.Length > 0 ? $" · RWY {piste}" : "") + (punti.Length > 0 ? $" · {punti}" : "");
            hits.Add((PesoTesto, Hit(docTitle, tipo, s, snippet, url)));
        }
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
