using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Le procedure di AD 2 (carta F7, slice 7a): i file 4 (STAR) e 6 (SID) di uno scalo. Le pagine sono carte (immagini, senza
/// testo: si contano e basta) o testo: titoli di sezione («SID RNAV1 or RNP1 RWY24 - Description»), designatori
/// («AGNIS 7G») e sotto ciascuno la tabella dei tratti, una riga per tratto; le convenzionali hanno prima la prosa e la riga
/// «MCA/MCL», o solo la prosa (le STAR). Più: la salita iniziale («Initial climb procedure RWY 06») e la WAYPOINT LIST, le
/// coordinate dei punti RNAV dello scalo.
/// <para>🔴 <b>Le colonne si prendono dalle parole dell'intestazione</b> («Path Terminator», «Waypoint Identifier»,
/// «Fly Over»…), non dalle righe disegnate: nei cinque scali le tabelle hanno 10, 11 o 12 colonne, in ordine diverso, e le
/// righe verticali comprendono cornici e barre di modifica. Una parola va alla colonna col centro più vicino.</para>
/// <para>Un tratto è una riga col <i>path terminator</i> nella prima colonna; la rotta magnetica sta qualche punto sopra
/// la sua linea di base e la vera (fra parentesi) qualche punto sotto: un tratto prende le parole fino a metà strada dal
/// tratto vicino.</para>
/// </summary>
public static partial class ItalianAd2Procedures
{
    private static readonly HashSet<string> PathTerminators = new(StringComparer.Ordinal)
        { "IF", "TF", "CF", "CA", "DF", "RF", "HM", "HA", "HF", "FA", "FC", "FD", "FM", "VA", "VI", "VM", "VD", "CI", "CD", "CR", "AF", "PI" };

    private enum Colonna { Path, Fix, FlyOver, Course, Variation, Distance, Turn, Altitude, Speed, Navigation }

    // La parola della prima riga dell'intestazione che dice la colonna (la seconda riga, «Terminator Identifier Over…», no:
    // «Identifier» è anche della WAYPOINT LIST).
    private static readonly Dictionary<string, Colonna> Intestazioni = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Path"] = Colonna.Path,
        ["Waypoint"] = Colonna.Fix,
        ["Fly"] = Colonna.FlyOver,
        ["Course"] = Colonna.Course,
        ["Magnetic"] = Colonna.Variation,
        ["Distance"] = Colonna.Distance,
        ["Turn"] = Colonna.Turn,
        ["Altitude"] = Colonna.Altitude,
        ["Speed"] = Colonna.Speed,
        ["Navigation"] = Colonna.Navigation,
    };

    [GeneratedRegex(@"^(?<k>S\s?I\s?Ds?|S\s?T\s?A\s?R|TRANSITION\s+SEGMENTS?\s+TO\s+ENROUTE)\b(?!.*\bsubject\s+to\b)(?=.*(?:RWY|DESCRIPTIO\s?N|[Dd]iscretion))", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxSezione();

    // Non la prima riga di una descrizione («Initial climb procedure completed, turn left…», LIEA e LIEE: passo 6); il titolo
    // anche al plurale e con le «l» al posto delle «I» («INlTlAL CLIMB PROCEDURES», LIEE 6).
    [GeneratedRegex(@"^In[il1]t[il1]al\s*cli\s*mb\s+procedures?\b(?![\s,]+(?:executed|completed))(?!\s+shall)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxSalitaIniziale();

    // Il designatore con le sue piste (passo 6): «SID BEROK 6B RWY 03R/L» (LIRP), «SID GILIO 5N (RWY 04C)» (LIRV).
    [GeneratedRegex(@"^(?:STAR|SID)\s+(?<nome>[A-Z]{3,5})\s?(?<num>\d{1,2}[A-Z])\s+\(?RWY\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxDesignatoreConLePiste();

    // Le piste in un titolo a sé, prima delle sezioni che non le dicono (passo 6): «DEPARTURES FROM RWY 02 – AEA VOR/DME» (LIEA),
    // «DEPARTURE FROM RWY 32 (VOR OPERATIVE)» (LIEE), «RWY 26» sotto «CATANIA FONTANAROSSA STAR RNAV 1» (LICC).
    [GeneratedRegex(@"^(?:(?:DEPARTURES?|ARRIVALS?)\s+(?:FROM|TO|FOR)\s+RWY\b|RWY\s*\d{2}\s*[LRC]?(?:\s*/\s*\d{0,2}\s*[LRC]?)*\s*$)", RegexOptions.CultureInvariant)]
    private static partial Regex RxPisteDelTitolo();

    [GeneratedRegex(@"^If\s+cleared\s+for\s+(?<c>.+?):?\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxCondizione();

    // Il genere davanti (passo 6: «STAR MIVKI 1D» a LIRP e LIEE) e le lettere spezzate («STAR K OVAS 1V», «STAR C AR 1 S»).
    [GeneratedRegex(@"^(?:(?:STAR|SID)\s+)?(?<nome>[A-Z](?:\s?[A-Z]){1,4})\s?(?<num>\d{1,2}\s?[A-Z])(?:\s*(?<x>\(.+\)))?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxDesignatore();

    // Il designatore dentro il titolo (passo 6, LIDT e gli eliporti): «Trento RNP AVCOL 5A Departure», «ARCO Hospital (LIKA) RNP
    // ALUCE 5A Departure».
    [GeneratedRegex(@"\b(?<nome>[A-Z]{3,5})\s(?<num>\d{1,2}[A-Z])\s+(?:Departure|Arrival|DEPARTURE|ARRIVAL)\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxDesignatoreNelTitolo();

    // Le pagine in due lingue degli scali militari (passo 6: LIBF, LIDT …): il designatore due volte sulla riga, l'italiano a
    // sinistra e l'inglese a destra («EKTOL 1A (a discrezione ATC) EKTOL 1A (ATC Discretion)», «OZE 5Z OZE 5Z»).
    [GeneratedRegex(@"^(?<d>[A-Z]{3,5}\s?\d{1,2}[A-Z])\b.*\s(?<e>\k<d>)\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxDesignatoreInDueLingue();

    // La WAYPOINT LIST nel formato ARINC 424 (passo 6, LIRP, LIEE, LIRV, gli eliporti): «MIVKI N44132743 E010041382»; anche col
    // punto prima dei centesimi di secondo (LIRA 6: «RA503 N415153.56 E0123258.45»).
    [GeneratedRegex(@"^(?<la>[NS])(?<lag>\d{2})(?<lam>\d{2})(?<las>\d{2})\.?(?<lac>\d{2})\s+(?<lo>[EW])(?<log>\d{3})(?<lom>\d{2})(?<los>\d{2})\.?(?<loc>\d{2})$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxCoordinateArinc();

    // La sigla di una radioassistenza dopo il punto, nella colonna dei punti: «ELB VOR» (LIRP 4).
    [GeneratedRegex(@"^(?<id>[A-Z]{2,3})\s+(?:VOR/DME|VORTAC|VOR|NDB|DME|TACAN)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxPuntoConIlTipo();

    [GeneratedRegex(@"^(?:WAYPOINT\s+LIST\b|Waypoint\s*$|RF\s+arc\s+centre|Waypoints?\s+Table\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxListaDeiPunti();

    // Le attese (passo 6) hanno la loro tabella, che si legge a parte (Attese): «HOLDING RWY 03R/L», «JESSY HOLDING PATTERN
    // (RNAV)», «RNAV HOLDING PATTERNS».
    [GeneratedRegex(@"^(?:NOISE\s+ABATEMENT\s+PROCEDURES?\b|WAYPOINT\s+HOLDING\b|HOLDING(?:\s+RNAV)?\s*$|HOLDINGS?\s+RWY\b|(?:[A-Z]{2,5}\s+|RNAV\s+)?HOLDING\s+PATTERNS?\b)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxSezioneDaSaltare();

    [GeneratedRegex(@"RWY\s*(?<p>\d{2})\s*(?<l>[LRC](?![A-Za-z])(?:\s*/\s*[LRC](?![A-Za-z]))*)?(?:\s*/\s*(?<q>\d{2})\s*(?<m>[LRC](?![A-Za-z])(?:\s*/\s*[LRC](?![A-Za-z]))*)?)?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxPiste();

    [GeneratedRegex(@"^(?:MCA|MCL|MEA|MEL)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxMinime();

    [GeneratedRegex(@"^(?<id>[A-Z]{2}\d{3,4}|[A-Z]{5})\s+(?<c>.*\d.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxPuntoDellaLista();

    [GeneratedRegex(@"^(?:AD\s*2\s+[A-Z]{4}\s+\d+\s*-\s*\d+|AIP\s*-\s*Italia.*|ENAV\s*-\s*Roma.*|\(A\d+/\d+\)|.*AIRAC effective date.*|\d{1,2}\s+[A-Z]{3}\s+\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex RxIntestazioneDiPagina();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    [GeneratedRegex(@"r\s*=\s*(?<r>\d+(?:[.,]\d+)?)\s*NM", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex RxRaggio();

    [GeneratedRegex(@"centre:?\s*(?<c>[A-Z]{2}\d{3,4}|[A-Z]{3,5})\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxCentro();

    [GeneratedRegex(@"([NS])\s*[–-]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex RxTrattinoFraLeCoordinate();

    /// <summary>
    /// Aggiunge i punti delle WAYPOINT LIST a quelli già letti, saltando quelli che ci sono già allo stesso posto (lo stesso
    /// punto sta nel file 4 e nel 6 dello stesso scalo). Uno con lo stesso nome e un altro posto entra: il controllo del
    /// modello lo dice.
    /// </summary>
    public static void AggiungiIPunti(List<SignificantPoint> punti, IEnumerable<SignificantPoint> nuovi)
    {
        ArgumentNullException.ThrowIfNull(punti);
        ArgumentNullException.ThrowIfNull(nuovi);
        foreach (var p in nuovi)
            if (!punti.Any(x => x.Designator == p.Designator && Math.Abs(x.Position.Lat - p.Position.Lat) < 1e-5 && Math.Abs(x.Position.Lon - p.Position.Lon) < 1e-5))
                punti.Add(p);
    }

    /// <summary>
    /// Aggiunge le attese lette a quelle già lette, saltando quelle di uno scalo che ci sono già (passo 6: la stessa attesa sta
    /// nella tabella delle STAR, file 4, e in quella degli avvicinamenti, file 5): stessa chiave e stessa quota minima.
    /// </summary>
    public static void AggiungiLeAttese(List<Holding> attese, IEnumerable<Holding> nuove)
    {
        ArgumentNullException.ThrowIfNull(attese);
        ArgumentNullException.ThrowIfNull(nuove);
        foreach (var h in nuove)
            if (!h.Section.StartsWith("AD ", StringComparison.Ordinal)
                || !attese.Any(x => AipCorrections.HoldingKey(x) == AipCorrections.HoldingKey(h) && x.Minimum.Text == h.Minimum.Text))
                attese.Add(h);
    }

    /// <summary>Vero se il PDF ha le pagine di testo delle procedure: una tabella dei tratti o un titolo di SID o STAR.</summary>
    public static bool EProcedure(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return pages.Any(p => Table.PlainLines(p).Any(l => l.Replace(" ", "", StringComparison.Ordinal).Contains("Terminator", StringComparison.Ordinal)
                                                          || RxSezione().IsMatch(l.Trim()) || RxDesignatoreInDueLingue().IsMatch(l.Trim())));
    }

    /// <summary>
    /// I designatori stampati nelle pagine di testo (passo 6, il conto del rapporto di lettura): le righe che sono un
    /// designatore (anche col genere davanti, le piste dopo, in due lingue) e i titoli «… AVCOL 5A Departure».
    /// </summary>
    internal static IReadOnlySet<string> DesignatoriStampati(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var nomi = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in pages.Where(p => p.Words.Count >= 30))
            foreach (var riga in Table.PlainLines(InInglese(page)).Select(l => RxSpazi().Replace(l, " ").Trim()))
            {
                var m = RxDesignatoreConLePiste().Match(riga);
                if (!m.Success) m = RxDesignatore().Match(riga);
                if (!m.Success) m = RxDesignatoreNelTitolo().Match(riga);
                if (m.Success) nomi.Add(Nome(m));
            }
        return nomi;
    }

    /// <summary>Il designatore com'è nel modello: «KOVAS 1V» anche da «K OVAS 1V», «CAR 1S» da «C AR 1 S».</summary>
    private static string Nome(Match m) =>
        $"{m.Groups["nome"].Value.Replace(" ", "", StringComparison.Ordinal)} {m.Groups["num"].Value.Replace(" ", "", StringComparison.Ordinal)}";

    /// <summary>
    /// Una pagina in due lingue (gli scali militari, passo 6: l'italiano a sinistra, l'inglese a destra, il designatore su
    /// tutte e due): si tiene la colonna inglese, che comincia dove comincia il designatore ripetuto. Le altre pagine come sono.
    /// </summary>
    private static PdfPageText InInglese(PdfPageText page)
    {
        var tagli = new List<double>();
        foreach (var (y, testo) in Table.PlainLinesWithY(page))
        {
            if (RxDesignatoreInDueLingue().Match(testo.Trim()) is not { Success: true } m) continue;
            var primaParola = m.Groups["d"].Value.Split(' ')[0];
            var stesse = page.Words.Where(w => Math.Abs(w.Baseline - y) <= 1.5 && w.Text.StartsWith(primaParola, StringComparison.Ordinal))
                .OrderBy(w => w.X0).ToList();
            if (stesse.Count >= 2) tagli.Add(stesse[1].X0);
        }
        if (tagli.Count == 0) return page;
        var taglio = tagli.Min() - 1;
        return page with { Words = [.. page.Words.Where(w => w.Baseline <= 40 || w.Baseline >= page.Height - 20 || w.XCenter >= taglio)] };
    }

    private sealed class InCorso(string designatore, ProcedureKind genere, IReadOnlyList<string> piste, string? sezione, ImportPage pagina, bool dalTitolo = false)
    {
        /// <summary>Una salita iniziale aperta dal titolo: se poi le condizioni («If cleared for…») la dividono, resta vuota e si lascia.</summary>
        public bool DalTitolo { get; } = dalTitolo;
        public string Designatore { get; } = designatore;
        public ProcedureKind Genere { get; } = genere;
        public IReadOnlyList<string> Piste { get; } = piste;
        public string? Sezione { get; } = sezione;
        public ImportPage Pagina { get; } = pagina;
        public List<ProcedureLeg> Tratti { get; } = [];
        public List<string> Prima { get; } = [];
        public List<string> Dopo { get; } = [];
        public List<string> Minime { get; } = [];
    }

    /// <summary>
    /// Le procedure del file 4 o 6 di uno scalo. <paramref name="genereDelFile"/>: STAR per il 4, SID per il 6, finché un titolo
    /// non dice altro. <paramref name="parte"/>: «AD 2» per gli aeroporti, «AD 3» per gli eliporti (la sezione dei punti e delle
    /// attese).
    /// </summary>
    public static ImportResult Read(IReadOnlyList<PdfPageText> pages, string icao, ProcedureKind genereDelFile, string parte = "AD 2")
    {
        ArgumentNullException.ThrowIfNull(pages);
        var pagine = new List<ImportPage>();
        var problemi = new List<ImportIssue>();
        var procedure = new List<InCorso>();
        var punti = new List<SignificantPoint>();
        var decimali = new Dictionary<string, int>(StringComparer.Ordinal);
        var attese = new List<Holding>();

        var genere = genereDelFile;
        IReadOnlyList<string> piste = [];
        IReadOnlyList<string> pisteDelTitolo = [];
        string? sezione = null;
        var modo = Modo.Procedure;
        InCorso? corrente = null;

        foreach (var stampata in pages)
        {
            var page = InInglese(stampata);
            var righe = Table.PlainLinesWithY(page);
            var testi = righe.Select(r => r.Text).ToList();
            var pagina = new ImportPage(page.Number, PageInfo.Reference(testi), PageInfo.Date(testi), Table: page.Words.Count >= 30);
            pagine.Add(pagina);
            if (!pagina.Table) continue;
            if (pagina.Effective is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));

            // La WAYPOINT LIST sta in fondo a una pagina: la pagina dopo ricomincia con le procedure.
            if (modo == Modo.Punti) modo = Modo.Procedure;
            var tratti = Tratti(page, righe, icao, problemi, $"{parte} {icao}", Pagina(pagina));
            var eventi = new List<(double Y, object Cosa)>();
            eventi.AddRange(tratti.Select(t => (t.Y, (object)t)));
            foreach (var (y, testo) in righe)
            {
                var t = RxSpazi().Replace(testo, " ").Trim();
                if (t.Length == 0 || RxIntestazioneDiPagina().IsMatch(t)) continue;
                // Le righe dentro una tabella (intestazione e tratti) non sono testo della procedura. Il designatore può stare a
                // 21 punti dalla riga «Terminator» (LICC 4 p.25): non è intestazione.
                if (tratti.Any(r => y >= r.Da && y <= r.A)) continue;
                if (tratti.Any(r => y >= r.Intestazione - 22 && y <= r.Intestazione + 8)
                    && !RxDesignatore().IsMatch(t) && !RxDesignatoreNelTitolo().IsMatch(t) && !RxDesignatoreConLePiste().IsMatch(t)) continue;
                eventi.Add((y, t));
            }

            foreach (var (_, cosa) in eventi.OrderBy(e => e.Y))
            {
                if (cosa is TrattoLetto letto)
                {
                    // Una riga della tabella di un'attesa: si legge in ogni sezione. La stessa attesa stampata più volte, una.
                    if (letto.Attesa is { } attesa)
                    {
                        if (!attese.Any(a => AipCorrections.HoldingKey(a) == AipCorrections.HoldingKey(attesa) && a.Minimum.Text == attesa.Minimum.Text))
                            attese.Add(attesa);
                        continue;
                    }
                    if (letto.Tratto is null || modo != Modo.Procedure) continue;
                    if (corrente is null)
                    {
                        problemi.Add(new ImportIssue(ImportIssueKind.Procedure, null, page.Number, $"un tratto fuori da una procedura: {letto.Tratto.PathTerminator} {letto.Tratto.Fix}"));
                        continue;
                    }
                    corrente.Tratti.Add(letto.Tratto);
                    problemi.AddRange(letto.CheNonVa.Select(c => new ImportIssue(ImportIssueKind.Procedure, corrente.Designatore, page.Number, c)));
                    continue;
                }

                var riga = (string)cosa;
                if (RxDesignatoreConLePiste().Match(riga) is { Success: true } conLePiste)
                {
                    if (genere == ProcedureKind.InitialClimb) genere = genereDelFile;
                    modo = Modo.Procedure;
                    corrente = new InCorso(Nome(conLePiste), genere, Piste(riga), sezione, pagina);
                    procedure.Add(corrente);
                    continue;
                }
                if (RxPisteDelTitolo().IsMatch(riga) && Piste(riga) is { Count: > 0 } delTitolo)
                {
                    pisteDelTitolo = piste = delTitolo;
                    continue;
                }
                // «STAR UPADU 1A (ATC Discretion)» (LIRV 4) è un designatore, non una sezione «… discretion».
                if (!RxDesignatore().IsMatch(riga) && RxSezione().Match(riga) is { Success: true } s)
                {
                    // PdfPig a volte spezza le parole: «STA R RWY06» (LIRN 4 p.15).
                    var k = s.Groups["k"].Value.Replace(" ", "", StringComparison.Ordinal);
                    genere = k.StartsWith("TRANSITION", StringComparison.Ordinal) ? ProcedureKind.Transition
                        : k.StartsWith("SID", StringComparison.Ordinal) ? ProcedureKind.Sid : ProcedureKind.Star;
                    // «SID DESCRIPTION» sotto «DEPARTURES FROM RWY 02»: le piste del titolo.
                    piste = Piste(riga) is { Count: > 0 } dellaSezione ? dellaSezione : pisteDelTitolo;
                    sezione = riga;
                    modo = Modo.Procedure;
                    corrente = null;
                    continue;
                }
                if (RxSalitaIniziale().IsMatch(riga))
                {
                    genere = ProcedureKind.InitialClimb;
                    piste = Piste(riga) is { Count: > 0 } dellaSalita ? dellaSalita : pisteDelTitolo;
                    sezione = riga;
                    modo = Modo.Procedure;
                    // Una pista può avere più salite iniziali (LIRF: la RNP e la convenzionale): la seconda è «RWY 25 (2)».
                    var nomeDellaSalita = piste.Count == 0 ? "RWY" : "RWY " + string.Join("/", piste);
                    var gia = procedure.Count(x => x.Genere == ProcedureKind.InitialClimb && (x.Designatore == nomeDellaSalita || x.Designatore.StartsWith(nomeDellaSalita + " (", StringComparison.Ordinal)));
                    corrente = new InCorso(gia == 0 ? nomeDellaSalita : $"{nomeDellaSalita} ({gia + 1})", genere, piste, sezione, pagina, dalTitolo: true);
                    procedure.Add(corrente);
                    continue;
                }
                if (RxListaDeiPunti().IsMatch(riga))
                {
                    modo = Modo.Punti;
                    continue;
                }
                if (RxSezioneDaSaltare().IsMatch(riga))
                {
                    modo = Modo.Salta;
                    corrente = null;
                    continue;
                }

                // Un designatore chiude la lista dei punti e la sezione saltata (le attese: una STAR può venire dopo).
                if (modo != Modo.Procedure && (RxDesignatore().IsMatch(riga) || RxDesignatoreNelTitolo().IsMatch(riga)))
                    modo = Modo.Procedure;
                // Un punto con le sue coordinate è un punto anche fuori da una «WAYPOINT LIST» col titolo (LIRF 6 p.32: la
                // tabella «Waypoint / Coordinates» e quella dei centri degli archi RF).
                var punto = RxPuntoDellaLista().Match(riga);
                // LIRN mette un trattino fra latitudine e longitudine («…N – 013°18'07.6647"E»).
                var coordinate = punto.Success ? RxTrattinoFraLeCoordinate().Replace(punto.Groups["c"].Value, "$1 ") : null;
                if (punto.Success && (modo == Modo.Punti || GeometryText.HasCoordinate(coordinate)))
                {
                    var id = punto.Groups["id"].Value;
                    // La lista si ripete sotto più procedure: un punto una volta sola, e due coordinate diverse si dicono.
                    if ((Arinc(coordinate) ?? GeometryText.ReadPoint(coordinate)) is not { } dove)
                        problemi.Add(new ImportIssue(ImportIssueKind.SignificantPoint, id, page.Number, riga));
                    else if (punti.FindIndex(x => x.Designator == id) is var k and >= 0)
                    {
                        // La stessa lista su più pagine, una coi secondi a un decimale e una a due (LIRF 6 p.20 e p.32): un
                        // punto entro 0,36" è lo stesso, e vale la scrittura più precisa. Oltre, due coordinate: si dice.
                        var gia = punti[k];
                        if (Math.Abs(gia.Position.Lat - dove.Lat) > 1e-4 || Math.Abs(gia.Position.Lon - dove.Lon) > 1e-4)
                            problemi.Add(new ImportIssue(ImportIssueKind.SignificantPoint, id, page.Number, $"due coordinate: {riga}"));
                        else if (Decimali(coordinate!) > decimali.GetValueOrDefault(id))
                        {
                            punti[k] = gia with { Position = dove, Page = Pagina(pagina) };
                            decimali[id] = Decimali(coordinate!);
                        }
                    }
                    else
                    {
                        punti.Add(new SignificantPoint { Section = $"{parte} {icao}", Designator = id, Position = dove, Page = Pagina(pagina) });
                        decimali[id] = Decimali(coordinate!);
                    }
                    continue;
                }
                if (modo == Modo.Punti) continue;
                if (modo == Modo.Salta) continue;

                if (genere == ProcedureKind.InitialClimb && RxCondizione().IsMatch(riga))
                {
                    corrente = new InCorso(riga.TrimEnd(':').Trim(), genere, piste, sezione, pagina);
                    procedure.Add(corrente);
                    continue;
                }
                if ((RxDesignatore().Match(riga) is { Success: true } trovato ? trovato : RxDesignatoreNelTitolo().Match(riga)) is { Success: true } d)
                {
                    // Un designatore dopo una salita iniziale, senza un titolo suo, è una procedura del file (LIRF 6 p.31: le SID
                    // RNP della 16R vengono subito dopo la loro salita iniziale).
                    if (genere == ProcedureKind.InitialClimb) genere = genereDelFile;
                    var nome = Nome(d);
                    // Il designatore ripetuto sotto il suo titolo («SID BEROK 6F RWY 21L/R» / «BEROK 6F», LIRP 6): la stessa procedura.
                    if (corrente is { Tratti.Count: 0, Prima.Count: 0, Minime.Count: 0 } vuota && vuota.Designatore == nome && !d.Groups["x"].Success) continue;
                    corrente = new InCorso(nome, genere, piste, sezione, pagina);
                    if (d.Groups["x"].Success) corrente.Prima.Add(d.Groups["x"].Value);
                    procedure.Add(corrente);
                    continue;
                }

                if (corrente is null) continue;
                if (RxMinime().IsMatch(riga)) corrente.Minime.Add(riga);
                else if (corrente.Tratti.Count == 0) corrente.Prima.Add(riga);
                else corrente.Dopo.Add(riga);
            }
        }

        var lette = new List<Procedure>();
        foreach (var p in procedure)
        {
            if (p.Tratti.Count == 0 && p.Prima.Count == 0 && p.Minime.Count == 0)
            {
                if (p.DalTitolo) continue;
                problemi.Add(new ImportIssue(ImportIssueKind.Procedure, p.Designatore, p.Pagina.Number, "né tratti né descrizione"));
                continue;
            }
            lette.Add(new Procedure
            {
                Aerodrome = icao,
                Kind = p.Genere,
                Designator = p.Designatore,
                Runways = p.Piste,
                Section = p.Sezione,
                Legs = p.Tratti,
                Description = Testo(p.Prima),
                Minimums = Testo(p.Minime),
                Remarks = Testo(p.Dopo),
                Page = Pagina(p.Pagina),
            });
        }
        return new ImportResult([], pagine, problemi) { Procedures = lette, Points = punti, Holdings = attese };
    }

    private enum Modo { Procedure, Punti, Salta }

    /// <summary>
    /// Una riga di tabella della pagina: un tratto, o un'attesa (la riga HM della tabella di un'attesa, passo 6).
    /// <paramref name="CheNonVa"/> si dice solo se il tratto entra in una procedura.
    /// </summary>
    private sealed record TrattoLetto(double Y, double Da, double A, double Intestazione, ProcedureLeg? Tratto, IReadOnlyList<string> CheNonVa, Holding? Attesa = null);

    /// <summary>I tratti e le attese della pagina, con la banda in altezza di ciascuno e la y della sua intestazione.</summary>
    private static List<TrattoLetto> Tratti(PdfPageText page, IReadOnlyList<(double Y, string Text)> righe, string icao, List<ImportIssue> problemi,
        string sezioneDelleAttese, AipPage? pagina)
    {
        // Le intestazioni: le righe con «Terminator»; le colonne dalle parole della riga sopra (Path, Waypoint, Fly…). Quella di
        // un'attesa ha «Inbound» (la rotta d'avvicinamento).
        var intestazioni = new List<(double Y, List<(Colonna Colonna, double X)> Colonne, bool Attesa)>();
        // «T erminator»: PdfPig a volte spezza la parola (LIMC 6 p.3).
        foreach (var (y, testo) in righe.Where(r => r.Text.Replace(" ", "", StringComparison.Ordinal).Contains("Terminator", StringComparison.Ordinal)))
        {
            var sopra = page.Words.Where(w => w.Baseline >= y - 22 && w.Baseline <= y + 2).ToList();
            var colonne = sopra
                .Where(w => Intestazione(w.Text) is not null)
                .GroupBy(w => Intestazione(w.Text)!.Value)
                .Select(g => (g.Key, g.OrderByDescending(w => w.Baseline).First().XCenter))
                .ToList();
            // La colonna Path sta sopra «Terminator», anche quando «Path» è spezzata («P ath», LIEA 6).
            if (colonne.All(c => c.Key != Colonna.Path)
                && sopra.FirstOrDefault(w => w.Text.StartsWith("Terminator", StringComparison.Ordinal) && Math.Abs(w.Baseline - y) <= 2) is { } terminatore)
                colonne.Add((Colonna.Path, terminatore.XCenter));
            var attesa = sopra.Any(w => w.Text.Equals("Inbound", StringComparison.OrdinalIgnoreCase));
            if (colonne.Count >= 5 && colonne.Any(c => c.Key == Colonna.Path)) intestazioni.Add((y, colonne, attesa));
        }
        if (intestazioni.Count == 0) return [];

        static Colonna? Intestazione(string parola) =>
            // Le colonne strette spezzano le parole dell'intestazione («Waypoin» / «t», «Cours» / «e», LIMC p.42); altre le
            // allungano («Flyover», «Course/Track»: LIDT, gli eliporti).
            Intestazioni.FirstOrDefault(i => i.Key.Equals(parola, StringComparison.OrdinalIgnoreCase)
                                             || parola.Length >= 4 && i.Key.StartsWith(parola, StringComparison.OrdinalIgnoreCase)
                                             || parola.StartsWith(i.Key, StringComparison.OrdinalIgnoreCase)) is { Key: not null } t ? t.Value : null;

        Colonna? DiChi(PdfWord w, List<(Colonna Colonna, double X)> colonne) =>
            colonne.MinBy(c => Math.Abs(c.X - w.XCenter)).Colonna;

        // Le righe dei tratti: un path terminator nella colonna Path, sotto un'intestazione.
        var basi = new List<(double Y, string Pt, (double Y, List<(Colonna Colonna, double X)> Colonne, bool Attesa) Intestazione)>();
        foreach (var w in page.Words.Where(w => PathTerminators.Contains(w.Text)).OrderBy(w => w.Baseline))
        {
            var sopra = intestazioni.Where(i => i.Y < w.Baseline - 3).OrderByDescending(i => i.Y).FirstOrDefault();
            if (sopra.Colonne is null || DiChi(w, sopra.Colonne) != Colonna.Path) continue;
            basi.Add((w.Baseline, w.Text, sopra));
        }

        var tratti = new List<TrattoLetto>();
        for (var i = 0; i < basi.Count; i++)
        {
            var (y, pt, (yIntestazione, colonne, diUnAttesa)) = basi[i];
            var da = Math.Max(Math.Max(y - 8, yIntestazione + 6), i > 0 ? (basi[i - 1].Y + y) / 2 : double.MinValue);
            // Fino a 10 punti sotto: in un tratto RF il punto sta sotto «RF centre:» (LIRF 6 p.31).
            var a = Math.Min(y + 10, i + 1 < basi.Count ? (y + basi[i + 1].Y) / 2 : double.MaxValue);
            if (diUnAttesa)
            {
                // Sotto l'intestazione di un'attesa, solo le righe d'attesa (LIMC 5 p.43: la tabella dei centri degli archi RF).
                if (pt is not ("HM" or "HA" or "HF")) continue;
                // La riga di un'attesa può essere alta (LIRF 4 p.8: «XIBIL» / «120» / «(ATC 10.8 L +FL190» / «DISCRETION)»).
                a = Math.Min(y + 26, i + 1 < basi.Count ? basi[i + 1].Y - 8 : double.MaxValue);
                var parole = page.Words.Where(w => w.Baseline > da && w.Baseline <= a).ToList();
                var xDelPunto = colonne.Where(c => c.Colonna == Colonna.Fix).Select(c => (double?)c.X).FirstOrDefault();
                var attesa = Attesa(parole, pt, sezioneDelleAttese, pagina, xDelPunto);
                if (attesa is null)
                    problemi.Add(new ImportIssue(ImportIssueKind.Holding, null, page.Number,
                        $"un'attesa senza rotta d'avvicinamento, verso o quota: {string.Join(' ', parole.OrderBy(w => w.X0).Select(w => w.Text))}"));
                tratti.Add(new TrattoLetto(y, da, a, yIntestazione, null, [], attesa));
                continue;
            }
            var celle = new Dictionary<Colonna, List<PdfWord>>();
            foreach (var w in page.Words.Where(w => w.Baseline > da && w.Baseline <= a))
            {
                if (DiChi(w, colonne) is not { } c) continue;
                if (!celle.TryGetValue(c, out var l)) celle[c] = l = [];
                l.Add(w);
            }
            string? Riga(Colonna c, Func<PdfWord, bool>? quali = null) =>
                celle.TryGetValue(c, out var l) && l.Where(quali ?? (_ => true)).OrderBy(w => w.Baseline).ThenBy(w => w.X0).Select(w => w.Text).ToList() is { Count: > 0 } t
                    ? Valore(string.Join(' ', t))
                    : null;
            // ⚠️ Non «sulla linea di base del path terminator»: in qualche tabella la sua cella è centrata più in basso del resto
            // della riga (LICA 6 p.3, LIRF 6 p.49: «DF» 5 punti sotto «CA501 … +2150»). Vale la banda del tratto.
            static bool Rimando(PdfWord w) => w.Text.StartsWith('(');
            var fix = Riga(Colonna.Fix, w => !Rimando(w));
            // «ELB VOR» (LIRP 4): il punto è la sigla.
            if (fix is not null && RxPuntoConIlTipo().Match(fix) is { Success: true } conIlTipo) fix = conIlTipo.Groups["id"].Value;
            var volo = Riga(Colonna.FlyOver, w => !Rimando(w));
            var noteDelVolo = Riga(Colonna.FlyOver, Rimando);
            var rotta = Riga(Colonna.Course, w => !w.Text.StartsWith('('));
            var vera = Riga(Colonna.Course, w => w.Text.StartsWith('('))?.Trim('(', ')');
            var distanza = Riga(Colonna.Distance);
            // La prima colonna di un tratto RF: «RF centre: RF620 r=2.1NM».
            // Il raggio sta sotto il punto, fino a 22 punti sotto la riga (LIRF 6 p.31: «r=2.1NM»).
            var finoA = Math.Min(y + 22, i + 1 < basi.Count ? basi[i + 1].Y - 3 : double.MaxValue);
            var arco = pt == "RF"
                ? string.Join(' ', page.Words.Where(w => w.Baseline > y - 3 && w.Baseline <= finoA && w.Text != pt && DiChi(w, colonne) == Colonna.Path)
                    .OrderBy(w => w.Baseline).ThenBy(w => w.X0).Select(w => w.Text))
                : null;
            var centro = arco is not null && RxCentro().Match(arco) is { Success: true } mc ? mc.Groups["c"].Value : null;
            var raggio = arco is not null && RxRaggio().Match(arco) is { Success: true } mr
                && double.TryParse(mr.Groups["r"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : (double?)null;
            var tratto = new ProcedureLeg
            {
                PathTerminator = pt,
                Fix = fix,
                FlyOver = volo is "Y" or "YES",
                Course = rotta,
                TrueCourse = vera,
                MagneticVariation = Riga(Colonna.Variation),
                DistanceNm = distanza is not null && double.TryParse(distanza, NumberStyles.Float, CultureInfo.InvariantCulture, out var nm) ? nm : null,
                Turn = Riga(Colonna.Turn),
                Altitude = Riga(Colonna.Altitude),
                Speed = Riga(Colonna.Speed),
                Navigation = Riga(Colonna.Navigation),
                Notes = noteDelVolo,
                ArcCenter = centro,
                ArcRadiusNm = raggio,
            };
            var cheNonVa = new List<string>();
            if (tratto.Fix is null && pt is "IF" or "TF" or "CF" or "DF" or "RF" or "HM")
                cheNonVa.Add($"{pt} senza punto (y {y:0})");
            if (pt == "RF" && (centro is null || raggio is null))
                cheNonVa.Add($"RF {fix}: centro o raggio dell'arco «{arco}»");
            if (distanza is not null && tratto.DistanceNm is null)
                cheNonVa.Add($"{pt} {fix}: distanza «{distanza}»");
            tratti.Add(new TrattoLetto(y, da, a, yIntestazione, tratto, cheNonVa));
        }
        return tratti;
    }

    // Anche col decimale («149.0°», LIRA 5) e col refuso «339°0» (LIRF 5 p.33).
    [GeneratedRegex(@"^(?<m>\d{3})(?:[.,]\d)?(?:[°º]\d?)?(?:\((?<v>\d{3}(?:[.,]\d+)?)[°º]?\))?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxRottaDellAttesa();

    // Un rimando attaccato al valore: «RMC(1)», «+6000(3)».
    [GeneratedRegex(@"^(?<v>.+?)(?<r>\(\d\))$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxRimandoAttaccato();

    [GeneratedRegex(@"^\((?<v>\d{3}(?:[.,]\d+)?)[°º]?\)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxRottaVera();

    // Anche la massima col meno davanti: «-6000» (LIBC, LICG).
    [GeneratedRegex(@"^[+-]?(?<q>FL\s?\d{2,3}|\d{4,5})\*?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxQuotaDellAttesa();

    [GeneratedRegex(@"^-?\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex RxVelocita();

    [GeneratedRegex(@"^(?:\(\d\))+$", RegexOptions.CultureInvariant)]
    private static partial Regex RxRimando();

    [GeneratedRegex(@"^\d+(?:FT|M)$", RegexOptions.CultureInvariant)]
    private static partial Regex RxQuotaConUnita();

    [GeneratedRegex(@"^[A-Z]{2,5}\d{0,4}$", RegexOptions.CultureInvariant)]
    private static partial Regex RxPuntoDellAttesa();

    /// <summary>
    /// Una riga della tabella di un'attesa (passo 6): le colonne cambiano da scalo a scalo (D e WD a LIRP, la quota massima e la
    /// declinazione a LIRN), e si leggono per forma, da sinistra: il punto, la rotta d'avvicinamento (e la vera fra parentesi),
    /// il tratto fino al verso della virata, poi la quota minima, la massima se c'è, la velocità, la specifica. La riga intera
    /// resta com'è in <see cref="Holding.Text"/>. Null se manca la rotta, il verso o la quota.
    /// </summary>
    private static Holding? Attesa(List<PdfWord> parole, string pt, string sezione, AipPage? pagina, double? xDelPunto)
    {
        // Il punto: il nome più vicino alla colonna «Waypoint» dell'intestazione; una nota in maiuscolo accanto alla riga ne ha
        // altri («HM MHA SUGGESTED … 6000 FT FOR TURBULENCE LOCRI», LIRA 4).
        static string SenzaRimando(string x) => RxRimandoAttaccato().Match(x) is { Success: true } m ? m.Groups["v"].Value : x;
        var nomi = parole.Where(w => w.Text != pt && RxPuntoDellAttesa().IsMatch(SenzaRimando(w.Text))).ToList();
        var vicino = xDelPunto is { } xp ? nomi.MinBy(w => Math.Abs(w.XCenter - xp)) : nomi.MinBy(w => w.X0);
        var punto = vicino is null ? null : SenzaRimando(vicino.Text);
        // «FL» e «105» in due parole: una.
        var t = new List<string>();
        foreach (var w in parole.OrderBy(w => w.X0).ThenBy(w => w.Baseline))
        {
            if (t.Count > 0 && t[^1] is "FL" or "+FL" && w.Text.All(char.IsAsciiDigit)) t[^1] += w.Text;
            else if (RxRimandoAttaccato().Match(w.Text) is { Success: true } attaccato && !w.Text.StartsWith('('))
                t.AddRange([attaccato.Groups["v"].Value, attaccato.Groups["r"].Value]);
            else t.Add(w.Text);
        }
        var testo = string.Join(' ', t);
        var i = t.IndexOf(pt);
        if (i < 0) return null;
        string? vera = null;
        int? rotta = null;
        var tratto = new List<string>();
        var rimandi = new List<string>();
        TurnDirection? verso = null;
        string? minima = null, massima = null, velocita = null;
        var specifica = new List<string>();
        foreach (var x in t.Skip(i + 1))
        {
            if (RxRimando().IsMatch(x)) { rimandi.Add(x); continue; }
            // Le note scritte dentro la riga («Suggested Path Terminator in case of coding», «MAX HLD 6000FT to remain inside
            // controlled airspace»: LIBC, LIBG, LICA) non sono valori; restano nel testo della riga.
            if (x.Any(char.IsLower) || RxQuotaConUnita().IsMatch(x)) continue;
            if (verso is null)
            {
                if (rotta is null && RxRottaDellAttesa().Match(x) is { Success: true } r)
                {
                    rotta = int.Parse(r.Groups["m"].Value, CultureInfo.InvariantCulture);
                    if (r.Groups["v"].Success) vera = r.Groups["v"].Value;
                }
                else if (RxRottaVera().Match(x) is { Success: true } v) vera ??= v.Groups["v"].Value;
                else if (x is "L" or "R") verso = x == "L" ? TurnDirection.Left : TurnDirection.Right;
                // Il tratto è fatto di numeri, trattini e barre («1/-», «- / 4.4», «3.0 4.2/-»).
                else if (rotta is not null && !x.Any(char.IsLetter)) tratto.Add(x);
                continue;
            }
            if (specifica.Count > 0 || x.StartsWith("RNAV", StringComparison.Ordinal) || x.StartsWith("RNP", StringComparison.Ordinal)) specifica.Add(x);
            else if (minima is null && RxQuotaDellAttesa().IsMatch(x)) minima = x;
            else if (minima is not null && massima is null && velocita is null && RxQuotaDellAttesa().IsMatch(x)) massima = x;
            else if (minima is not null && velocita is null && RxVelocita().IsMatch(x)) velocita = x;
        }
        if (punto is null || rotta is null || verso is null || minima is null) return null;
        static VerticalLimit Quota(string q) =>
            ItalianEnr3.QuotaDiAttesa(RxQuotaDellAttesa().Match(q).Groups["q"].Value.Replace(" ", "", StringComparison.Ordinal)) with { Text = q };
        var legato = string.Join(' ', tratto.Where(x => x != "-"));
        return new Holding
        {
            Section = sezione,
            Fix = punto,
            Minimum = Quota(minima),
            Turn = verso.Value,
            InboundTrack = rotta.Value,
            TrueTrack = vera,
            Leg = string.Join(' ', tratto) is { Length: > 0 } l && legato.Length > 0 ? l : null,
            Maximum = massima is null ? null : Quota(massima),
            Speed = velocita,
            Navigation = specifica.Count == 0 ? null : string.Join(' ', specifica),
            NoteRefs = rimandi.Count == 0 ? null : string.Concat(rimandi),
            Text = testo,
            Page = pagina,
        };
    }

    /// <summary>Le coordinate nel formato ARINC 424 della WAYPOINT LIST: «N44132743 E010041382» = 44°13'27.43" N 10°04'13.82" E.</summary>
    private static Position? Arinc(string? coordinate)
    {
        if (coordinate is null || RxCoordinateArinc().Match(coordinate.Trim()) is not { Success: true } m) return null;
        double Gradi(string g, string p, string s, string c) =>
            int.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture) + int.Parse(m.Groups[p].Value, CultureInfo.InvariantCulture) / 60d
            + (int.Parse(m.Groups[s].Value, CultureInfo.InvariantCulture) + int.Parse(m.Groups[c].Value, CultureInfo.InvariantCulture) / 100d) / 3600d;
        var lat = Gradi("lag", "lam", "las", "lac") * (m.Groups["la"].Value == "S" ? -1 : 1);
        var lon = Gradi("log", "lom", "los", "loc") * (m.Groups["lo"].Value == "W" ? -1 : 1);
        return new Position(lat, lon);
    }

    /// <summary>Il testo di una cella: «-» è vuoto.</summary>
    private static string? Valore(string testo)
    {
        var t = RxSpazi().Replace(testo, " ").Trim();
        return t is "" or "-" or "–" ? null : t;
    }

    /// <summary>Le piste del titolo: «RWY 16 L/R» → 16L, 16R; «RWY 10/28» → 10, 28.</summary>
    internal static IReadOnlyList<string> Piste(string titolo)
    {
        var piste = new List<string>();
        foreach (Match m in RxPiste().Matches(titolo))
        {
            void Aggiungi(string numero, Group lati)
            {
                if (lati.Success)
                    piste.AddRange(lati.Value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(l => numero + l));
                else
                    piste.Add(numero);
            }
            Aggiungi(m.Groups["p"].Value, m.Groups["l"]);
            if (m.Groups["q"].Success) Aggiungi(m.Groups["q"].Value, m.Groups["m"]);
        }
        return piste.Distinct().ToList();
    }

    /// <summary>Quante cifre dopo la virgola hanno i secondi più precisi della coordinata.</summary>
    private static int Decimali(string coordinata) =>
        RxDecimali().Matches(coordinata).Select(m => m.Length - 1).DefaultIfEmpty(0).Max();

    [GeneratedRegex(@"[.,]\d+", RegexOptions.CultureInvariant)]
    private static partial Regex RxDecimali();

    private static string? Testo(List<string> righe) => righe.Count == 0 ? null : string.Join('\n', righe);

    private static AipPage? Pagina(ImportPage p) => p.Reference is null ? null : new AipPage(p.Reference, p.Effective);
}
