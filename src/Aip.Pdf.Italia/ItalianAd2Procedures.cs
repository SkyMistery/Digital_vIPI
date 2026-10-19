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

    [GeneratedRegex(@"^Initial\s*cli\s*mb\s+procedure\b(?!\s+executed)(?!s?\s+shall)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxSalitaIniziale();

    [GeneratedRegex(@"^If\s+cleared\s+for\s+(?<c>.+?):?\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxCondizione();

    [GeneratedRegex(@"^(?<nome>[A-Z](?:\s?[A-Z]){1,4})\s?(?<num>\d{1,2}[A-Z])(?:\s*(?<x>\(.+\)))?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxDesignatore();

    [GeneratedRegex(@"^(?:WAYPOINT\s+LIST\b|Waypoint\s*$|RF\s+arc\s+centre)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxListaDeiPunti();

    [GeneratedRegex(@"^(?:NOISE\s+ABATEMENT\s+PROCEDURES?\b|WAYPOINT\s+HOLDING\b|HOLDING(?:\s+RNAV)?\s*$)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
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

    /// <summary>Vero se il PDF ha le pagine di testo delle procedure: una tabella dei tratti o un titolo di SID o STAR.</summary>
    public static bool EProcedure(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return pages.Any(p => Table.PlainLines(p).Any(l => l.Replace(" ", "", StringComparison.Ordinal).Contains("Terminator", StringComparison.Ordinal) || RxSezione().IsMatch(l.Trim())));
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

    /// <summary>Le procedure del file 4 o 6 di uno scalo. <paramref name="genereDelFile"/>: STAR per il 4, SID per il 6, finché un titolo non dice altro.</summary>
    public static ImportResult Read(IReadOnlyList<PdfPageText> pages, string icao, ProcedureKind genereDelFile)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var pagine = new List<ImportPage>();
        var problemi = new List<ImportIssue>();
        var procedure = new List<InCorso>();
        var punti = new List<SignificantPoint>();
        var decimali = new Dictionary<string, int>(StringComparer.Ordinal);

        var genere = genereDelFile;
        IReadOnlyList<string> piste = [];
        string? sezione = null;
        var modo = Modo.Procedure;
        InCorso? corrente = null;

        foreach (var page in pages)
        {
            var righe = Table.PlainLinesWithY(page);
            var testi = righe.Select(r => r.Text).ToList();
            var pagina = new ImportPage(page.Number, PageInfo.Reference(testi), PageInfo.Date(testi), Table: page.Words.Count >= 30);
            pagine.Add(pagina);
            if (!pagina.Table) continue;
            if (pagina.Effective is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));

            // La WAYPOINT LIST sta in fondo a una pagina: la pagina dopo ricomincia con le procedure.
            if (modo == Modo.Punti) modo = Modo.Procedure;
            var tratti = Tratti(page, righe, icao, problemi);
            var eventi = new List<(double Y, object Cosa)>();
            eventi.AddRange(tratti.Select(t => (t.Y, (object)t)));
            foreach (var (y, testo) in righe)
            {
                var t = RxSpazi().Replace(testo, " ").Trim();
                if (t.Length == 0 || RxIntestazioneDiPagina().IsMatch(t)) continue;
                // Le righe dentro una tabella (intestazione e tratti) non sono testo della procedura.
                if (tratti.Any(r => y >= r.Da && y <= r.A) || tratti.Any(r => y >= r.Intestazione - 22 && y <= r.Intestazione + 8)) continue;
                eventi.Add((y, t));
            }

            foreach (var (_, cosa) in eventi.OrderBy(e => e.Y))
            {
                if (cosa is TrattoLetto letto)
                {
                    if (modo != Modo.Procedure) continue;
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
                if (RxSezione().Match(riga) is { Success: true } s)
                {
                    // PdfPig a volte spezza le parole: «STA R RWY06» (LIRN 4 p.15).
                    var k = s.Groups["k"].Value.Replace(" ", "", StringComparison.Ordinal);
                    genere = k.StartsWith("TRANSITION", StringComparison.Ordinal) ? ProcedureKind.Transition
                        : k.StartsWith("SID", StringComparison.Ordinal) ? ProcedureKind.Sid : ProcedureKind.Star;
                    piste = Piste(riga);
                    sezione = riga;
                    modo = Modo.Procedure;
                    corrente = null;
                    continue;
                }
                if (RxSalitaIniziale().IsMatch(riga))
                {
                    genere = ProcedureKind.InitialClimb;
                    piste = Piste(riga);
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

                if (modo == Modo.Punti && RxDesignatore().IsMatch(riga))
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
                    if (GeometryText.ReadPoint(coordinate) is not { } dove)
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
                        punti.Add(new SignificantPoint { Section = $"AD 2 {icao}", Designator = id, Position = dove, Page = Pagina(pagina) });
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
                if (RxDesignatore().Match(riga) is { Success: true } d)
                {
                    // Un designatore dopo una salita iniziale, senza un titolo suo, è una procedura del file (LIRF 6 p.31: le SID
                    // RNP della 16R vengono subito dopo la loro salita iniziale).
                    if (genere == ProcedureKind.InitialClimb) genere = genereDelFile;
                    corrente = new InCorso($"{d.Groups["nome"].Value.Replace(" ", "", StringComparison.Ordinal)} {d.Groups["num"].Value}", genere, piste, sezione, pagina);
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
        return new ImportResult([], pagine, problemi) { Procedures = lette, Points = punti };
    }

    private enum Modo { Procedure, Punti, Salta }

    /// <summary>Un tratto della pagina; <paramref name="CheNonVa"/> si dice solo se il tratto entra in una procedura (non in un'attesa).</summary>
    private sealed record TrattoLetto(double Y, double Da, double A, double Intestazione, ProcedureLeg Tratto, IReadOnlyList<string> CheNonVa);

    /// <summary>I tratti della pagina, con la banda in altezza di ciascuno e la y della sua intestazione.</summary>
    private static List<TrattoLetto> Tratti(PdfPageText page, IReadOnlyList<(double Y, string Text)> righe, string icao, List<ImportIssue> problemi)
    {
        // Le intestazioni: le righe con «Terminator»; le colonne dalle parole della riga sopra (Path, Waypoint, Fly…).
        var intestazioni = new List<(double Y, List<(Colonna Colonna, double X)> Colonne)>();
        // «T erminator»: PdfPig a volte spezza la parola (LIMC 6 p.3).
        foreach (var (y, testo) in righe.Where(r => r.Text.Replace(" ", "", StringComparison.Ordinal).Contains("Terminator", StringComparison.Ordinal)))
        {
            var colonne = page.Words
                .Where(w => w.Baseline >= y - 22 && w.Baseline <= y + 2 && Intestazione(w.Text) is not null)
                .GroupBy(w => Intestazione(w.Text)!.Value)
                .Select(g => (g.Key, g.OrderByDescending(w => w.Baseline).First().XCenter))
                .ToList();
            if (colonne.Count >= 5 && colonne.Any(c => c.Key == Colonna.Path)) intestazioni.Add((y, colonne));
        }
        if (intestazioni.Count == 0) return [];

        static Colonna? Intestazione(string parola) =>
            // Le colonne strette spezzano le parole dell'intestazione («Waypoin» / «t», «Cours» / «e», LIMC p.42).
            Intestazioni.FirstOrDefault(i => i.Key.Equals(parola, StringComparison.OrdinalIgnoreCase)
                                             || parola.Length >= 4 && i.Key.StartsWith(parola, StringComparison.OrdinalIgnoreCase)) is { Key: not null } t ? t.Value : null;

        Colonna? DiChi(PdfWord w, List<(Colonna Colonna, double X)> colonne) =>
            colonne.MinBy(c => Math.Abs(c.X - w.XCenter)).Colonna;

        // Le righe dei tratti: un path terminator nella colonna Path, sotto un'intestazione.
        var basi = new List<(double Y, string Pt, (double Y, List<(Colonna Colonna, double X)> Colonne) Intestazione)>();
        foreach (var w in page.Words.Where(w => PathTerminators.Contains(w.Text)).OrderBy(w => w.Baseline))
        {
            var sopra = intestazioni.Where(i => i.Y < w.Baseline - 3).OrderByDescending(i => i.Y).FirstOrDefault();
            if (sopra.Colonne is null || DiChi(w, sopra.Colonne) != Colonna.Path) continue;
            basi.Add((w.Baseline, w.Text, sopra));
        }

        var tratti = new List<TrattoLetto>();
        for (var i = 0; i < basi.Count; i++)
        {
            var (y, pt, (yIntestazione, colonne)) = basi[i];
            var da = Math.Max(Math.Max(y - 8, yIntestazione + 6), i > 0 ? (basi[i - 1].Y + y) / 2 : double.MinValue);
            // Fino a 10 punti sotto: in un tratto RF il punto sta sotto «RF centre:» (LIRF 6 p.31).
            var a = Math.Min(y + 10, i + 1 < basi.Count ? (y + basi[i + 1].Y) / 2 : double.MaxValue);
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
