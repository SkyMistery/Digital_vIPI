using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>A quale gruppo del blocco «Documenti collegati» appartiene un link. L'ordine dell'enum è l'ordine a
/// schermo, chiesto dal committente: prima la vIPI ACC, poi gli APP, poi gli aeroporti.</summary>
public enum DocLinkGroup { Acc, App, Airport }

/// <summary>
/// Un documento a cui un link può portare. Porta tutto quel che serve a disegnarlo <b>senza</b> tornare al
/// database: famiglia e chiave per l'indirizzo, ACC per la rotta, id del documento per sapere se è pubblico
/// adesso e se sta nella stessa pagina unita.
/// </summary>
public sealed class DocLinkTarget
{
    public ReleaseTargetType Type { get; set; }
    public string Key { get; set; } = "";
    /// <summary>L'ACC della ROTTA (<c>/services/vsop/{acc}/…</c>), non per forza quello del documento aperto.</summary>
    public string AccCode { get; set; } = "";
    public int DocumentId { get; set; }
    /// <summary>Quel che si legge: <c>LIRR vIPI</c>, <c>LIBN_APP</c>, <c>LICZ vSOP</c>…</summary>
    public string Label { get; set; } = "";
    /// <summary>L'ancora dentro il documento bersaglio, senza cancelletto. Oggi solo la sezione di un APP
    /// remotizzato nella vIPI ACC (<see cref="DocumentiCollegati.AncoraApp"/>).</summary>
    public string? Anchor { get; set; }
}

/// <summary>
/// Un POSTO del blocco: una riga a schermo, con le sue <b>alternative in ordine</b>. Al disegno vince la prima
/// alternativa pubblica adesso; nessuna pubblica = la riga non c'è.
///
/// <para>⚠️ È la forma della scelta A (carta §A109 §3): la struttura si congela alla pubblicazione, ma chi dei
/// candidati si vede lo si decide al disegno. Così un APP senza documento pubblico cede il posto a quello sopra, e
/// uno scalo in bozza quando si è pubblicata la vIPI ACC compare da solo il giorno che viene pubblicato.</para>
/// </summary>
public sealed class DocLinkSlot
{
    public DocLinkGroup Group { get; set; }
    public List<DocLinkTarget> Alternatives { get; set; } = new();
}

/// <summary>I collegamenti congelati in una release (<see cref="DocReleasePayload.Collegamenti"/>).</summary>
public sealed class DocLinkSnapshot
{
    public List<DocLinkSlot> Slots { get; set; } = new();
}

/// <summary>Un link pronto da disegnare: il posto ha già scelto la sua alternativa.</summary>
public sealed record ResolvedDocLink(DocLinkGroup Group, DocLinkTarget Target, string Href);

/// <summary>Uno scalo come lo vede la struttura: il padre scritto, le sue posizioni visibili, l'ACC d'anagrafica.</summary>
/// <param name="Positions">Le posizioni dello scalo (ATIS e nascoste escluse): da lì parte la risalita.</param>
public sealed record DocLinkAirport(string Icao, string? AccCode, bool IsHidden, string? ParentCallsign,
    IReadOnlyList<string> Positions);

/// <summary>Un settore APP d'anagrafica: remotizzato o no, e il documento che lo descrive (se ce l'ha).</summary>
public sealed record DocLinkApp(string Callsign, bool Remotized, int? DocumentId, string? AccCode);

/// <summary>
/// Tutto quel che serve a decidere i collegamenti: la struttura (albero EFFETTIVO) e i documenti.
/// </summary>
/// <param name="ParentOf">Callsign → padre effettivo (<c>EffectiveHierarchy.ParentMap</c>).</param>
/// <param name="CenterOf">Settore ACC → codice dell'ACC (<c>AccSector.CenterId</c>).</param>
/// <param name="Docs">I documenti come li descrive il registro unico (<c>IDocumentAdminRepository.ListAsync</c>).</param>
public sealed record DocLinkGraph(
    IReadOnlyDictionary<string, string?> ParentOf,
    IReadOnlyDictionary<string, string> CenterOf,
    IReadOnlyList<DocLinkAirport> Airports,
    IReadOnlyDictionary<string, DocLinkApp> Apps,
    IReadOnlyList<ManagedDoc> Docs);

/// <summary>
/// Le regole dei documenti collegati (carta <c>docs/feature/2026-09-21-documenti-collegati.md</c>). Pure: niente
/// database, niente ora — la struttura entra come <see cref="DocLinkGraph"/>, la visibilità come funzione.
/// </summary>
public static class DocumentiCollegati
{
    /// <summary>L'ancora della sezione di un APP remotizzato nella vIPI ACC. ⚠️ La pagina ACC deve metterla
    /// sull'intestazione del gruppo che contiene quell'APP, o il link apre la vIPI in cima senza errore.</summary>
    public static string AncoraApp(string callsign) => $"app-{callsign.ToUpperInvariant()}";

    /// <summary>I posti del blocco per il documento (<paramref name="type"/>, <paramref name="key"/>).</summary>
    public static DocLinkSnapshot Capture(DocLinkGraph g, ReleaseTargetType type, string key)
    {
        var ctx = new Contesto(g);
        var slots = type switch
        {
            ReleaseTargetType.AccVipi => ctx.PerAcc(key.Split('|', 2)[0]),
            ReleaseTargetType.App => ctx.PerApp(key),
            ReleaseTargetType.Airport or ReleaseTargetType.AirportMil => ctx.PerScalo(key, type),
            ReleaseTargetType.Vloa => ctx.PerVloa(key),
            _ => new List<DocLinkSlot>(),
        };
        return new DocLinkSnapshot { Slots = slots };
    }

    /// <summary>
    /// Sceglie, per ogni posto, la prima alternativa visibile. L'ordine dei gruppi è quello dell'enum; dentro un
    /// gruppo resta quello congelato. Un documento non compare due volte.
    /// </summary>
    public static IReadOnlyList<(DocLinkGroup Group, DocLinkTarget Target)> Resolve(
        DocLinkSnapshot snap, Func<DocLinkTarget, bool> visibile)
    {
        var visti = new HashSet<(int, string?)>();
        var fuori = new List<(DocLinkGroup, DocLinkTarget)>();
        foreach (var slot in snap.Slots.OrderBy(s => s.Group))   // OrderBy è stabile
        {
            var t = slot.Alternatives.FirstOrDefault(visibile);
            if (t is not null && visti.Add((t.DocumentId, t.Anchor))) fuori.Add((slot.Group, t));
        }
        return fuori;
    }

    private sealed class Contesto
    {
        private readonly DocLinkGraph _g;
        private readonly Dictionary<string, DocLinkAirport> _scali;
        private readonly Dictionary<string, List<string>> _catene = new(StringComparer.OrdinalIgnoreCase);

        public Contesto(DocLinkGraph g)
        {
            _g = g;
            _scali = g.Airports.GroupBy(a => a.Icao, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        }

        // ---- la struttura -------------------------------------------------------------------------

        /// <summary>Da <paramref name="partenza"/> in su, lei compresa. ⚠️ Con la guardia sui nodi già visti: un
        /// anello nell'albero tronca la catena in silenzio, come in ogni altro lettore (il report lo segnala).</summary>
        private List<string> Risali(string? partenza)
        {
            var catena = new List<string>();
            var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var c = partenza; !string.IsNullOrWhiteSpace(c) && visti.Add(c!); c = _g.ParentOf.GetValueOrDefault(c!))
                catena.Add(c!);
            return catena;
        }

        /// <summary>La catena di uno scalo: dalla sua posizione più bassa in su. La più LUNGA fra quelle che partono
        /// dalle sue posizioni è quella che parte dalla più bassa, perché la scaletta DEL→GND→TWR→APP è lineare.
        /// Senza posizioni si parte dal padre scritto dello scalo.</summary>
        private List<string> CatenaDi(DocLinkAirport a)
        {
            if (_catene.TryGetValue(a.Icao, out var c)) return c;
            c = a.Positions.Select(Risali).OrderByDescending(x => x.Count).FirstOrDefault()
                ?? new List<string>();
            if (c.Count == 0) c = Risali(a.ParentCallsign);
            return _catene[a.Icao] = c;
        }

        /// <summary>L'ACC di una catena: il primo settore ACC che incontra; altrimenti l'anagrafica.</summary>
        private string? AccDi(IReadOnlyList<string> catena, string? ripiego) =>
            catena.Select(c => _g.CenterOf.GetValueOrDefault(c)).FirstOrDefault(x => x is not null) ?? ripiego;

        /// <summary>La parte della catena che sta SOTTO il primo settore ACC: è lì che si cerca l'APP.</summary>
        private IEnumerable<string> SottoAcc(IEnumerable<string> catena) =>
            catena.TakeWhile(c => !_g.CenterOf.ContainsKey(c));

        // ---- i documenti --------------------------------------------------------------------------

        private ManagedDoc? Doc(ReleaseTargetType tipo, Func<ManagedDoc, bool> filtro) =>
            _g.Docs.Where(d => d.Kind == tipo && d.DocumentId is not null && filtro(d))
                .OrderBy(d => d.ReleaseKey, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        private ManagedDoc? DocAcc(string? acc) => acc is null ? null
            : Doc(ReleaseTargetType.AccVipi, d => string.Equals(d.AccCode, acc, StringComparison.OrdinalIgnoreCase));

        private ManagedDoc? DocScalo(ReleaseTargetType tipo, string icao) =>
            Doc(tipo, d => string.Equals(d.ReleaseKey, icao, StringComparison.OrdinalIgnoreCase));

        private static DocLinkTarget Bersaglio(ManagedDoc d, string label, string? acc = null, string? ancora = null) => new()
        {
            Type = d.ReleaseTarget, Key = d.ReleaseKey, AccCode = (acc ?? d.AccCode ?? "").ToUpperInvariant(),
            DocumentId = d.DocumentId!.Value, Label = label, Anchor = ancora,
        };

        private DocLinkTarget? VipiAcc(string? acc) =>
            DocAcc(acc) is { } d ? Bersaglio(d, $"{d.AccCode!.ToUpperInvariant()} vIPI") : null;

        private DocLinkTarget? Scalo(ReleaseTargetType tipo, string icao) =>
            DocScalo(tipo, icao) is { } d
                ? Bersaglio(d, $"{icao.ToUpperInvariant()} {(tipo == ReleaseTargetType.AirportMil ? "vSOP" : "vIPI")}")
                : null;

        /// <summary>L'APP che ha un documento: remotizzato = la sua sezione nella vIPI ACC; non remotizzato = il suo
        /// documento; nessun documento = null (e si sale ancora).</summary>
        private DocLinkTarget? AppConDocumento(string callsign)
        {
            if (!_g.Apps.TryGetValue(callsign, out var app)) return null;
            if (app.Remotized)
                return DocAcc(app.AccCode) is { } acc
                    ? Bersaglio(acc, $"{acc.AccCode!.ToUpperInvariant()} vIPI · {app.Callsign.ToUpperInvariant()}",
                        ancora: AncoraApp(app.Callsign))
                    : null;
            return app.DocumentId is int id && Doc(ReleaseTargetType.App, d => d.DocumentId == id) is { } doc
                ? Bersaglio(doc, doc.ReleaseKey.ToUpperInvariant())
                : null;
        }

        private static DocLinkSlot Posto(DocLinkGroup g, params DocLinkTarget?[] alt) =>
            new() { Group = g, Alternatives = alt.Where(t => t is not null).Select(t => t!).ToList() };

        private static void Aggiungi(List<DocLinkSlot> slots, DocLinkSlot s)
        {
            if (s.Alternatives.Count > 0) slots.Add(s);
        }

        // ---- per famiglia -------------------------------------------------------------------------

        /// <summary>vIPI ACC: gli APP non remotizzati e TUTTI gli scali il cui ACC risolto è questo.</summary>
        public List<DocLinkSlot> PerAcc(string acc)
        {
            var slots = new List<DocLinkSlot>();

            var app = _g.Docs.Where(d => d.Kind == ReleaseTargetType.App && d.DocumentId is not null)
                .Where(d => string.Equals(AccDi(Risali(d.ReleaseKey), d.AccCode), acc, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.ReleaseKey, StringComparer.OrdinalIgnoreCase);
            foreach (var d in app) Aggiungi(slots, Posto(DocLinkGroup.App, Bersaglio(d, d.ReleaseKey.ToUpperInvariant())));

            foreach (var a in _g.Airports.Where(a => !a.IsHidden).OrderBy(a => a.Icao, StringComparer.OrdinalIgnoreCase))
                if (string.Equals(AccDi(CatenaDi(a), a.AccCode), acc, StringComparison.OrdinalIgnoreCase))
                    // Vince la vIPI; il vSOP solo se la vIPI non c'è (o non è pubblica adesso).
                    Aggiungi(slots, Posto(DocLinkGroup.Airport,
                        Scalo(ReleaseTargetType.Airport, a.Icao), Scalo(ReleaseTargetType.AirportMil, a.Icao)));
            return slots;
        }

        /// <summary>APP non remotizzato: la vIPI dell'ACC e, per ogni scalo sotto uno qualunque dei suoi settori,
        /// vIPI e vSOP (quelle che esistono).</summary>
        public List<DocLinkSlot> PerApp(string callsign)
        {
            var slots = new List<DocLinkSlot>();
            var doc = Doc(ReleaseTargetType.App, d => string.Equals(d.ReleaseKey, callsign, StringComparison.OrdinalIgnoreCase));
            if (doc is null) return slots;

            Aggiungi(slots, Posto(DocLinkGroup.Acc, VipiAcc(AccDi(Risali(callsign), doc.AccCode))));

            // Tutti i settori del documento, non solo il principale: LIBV_APP e LIBV_G_APP possono stare nello stesso.
            var miei = _g.Apps.Values.Where(a => a.DocumentId == doc.DocumentId).Select(a => a.Callsign)
                .Append(callsign).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var a in _g.Airports.Where(a => !a.IsHidden).OrderBy(a => a.Icao, StringComparer.OrdinalIgnoreCase))
                if (SottoAcc(CatenaDi(a)).Any(miei.Contains))
                {
                    Aggiungi(slots, Posto(DocLinkGroup.Airport, Scalo(ReleaseTargetType.Airport, a.Icao)));
                    Aggiungi(slots, Posto(DocLinkGroup.Airport, Scalo(ReleaseTargetType.AirportMil, a.Icao)));
                }
            return slots;
        }

        /// <summary>vIPI o vSOP di uno scalo: la vIPI dell'ACC, l'APP che lo controlla, l'altra edizione.</summary>
        public List<DocLinkSlot> PerScalo(string icao, ReleaseTargetType tipo)
        {
            var slots = new List<DocLinkSlot>();
            var a = _scali.GetValueOrDefault(icao);
            var catena = a is null ? new List<string>() : CatenaDi(a);

            Aggiungi(slots, Posto(DocLinkGroup.Acc, VipiAcc(AccDi(catena, a?.AccCode))));

            // UN posto per l'APP, con tutti gli APP della catena come alternative, dal più vicino: se il più vicino
            // non ha un documento pubblico si sale — ma lo decide il disegno, non la pubblicazione.
            Aggiungi(slots, Posto(DocLinkGroup.App, SottoAcc(catena).Select(AppConDocumento).ToArray()));

            var altra = tipo == ReleaseTargetType.AirportMil ? ReleaseTargetType.Airport : ReleaseTargetType.AirportMil;
            Aggiungi(slots, Posto(DocLinkGroup.Airport, Scalo(altra, icao)));
            return slots;
        }

        /// <summary>vLOA: la vIPI degli ACC coinvolti che ne hanno una — cioè gli italiani, perché solo loro ne
        /// hanno.</summary>
        public List<DocLinkSlot> PerVloa(string key)
        {
            var slots = new List<DocLinkSlot>();
            if (!int.TryParse(key, out var id)) return slots;
            var doc = _g.Docs.FirstOrDefault(d => d.Kind == ReleaseTargetType.Vloa && d.DocumentId == id);
            if (doc is null) return slots;

            foreach (var acc in new[] { doc.AccCode, doc.NeighbourCode }
                         .Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
                Aggiungi(slots, Posto(DocLinkGroup.Acc, VipiAcc(acc)));
            return slots;
        }
    }
}
