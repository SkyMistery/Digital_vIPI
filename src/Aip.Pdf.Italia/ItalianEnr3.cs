using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// ENR 3 dell'AIP italiano (carta F6, slice 2): le rotte ATS (un PDF per rotta, <c>LI-ENR 3.2 L12</c>) e le attese in
/// rotta (<c>LI-ENR 3.4.1</c>).
/// <para><b>Le rotte</b> sono una tabella di 7 colonne (8 righe verticali, pagine pari e dispari sfalsate): punto ·
/// rotta magnetica e distanza · limiti verticali · ampiezza · livelli dispari (verso e MNM FL) · livelli pari · note. Un
/// punto è una riga che comincia con ▲ (obbligatorio) o ∆ (a richiesta), con le coordinate sotto; il tratto verso il
/// punto dopo sta fra i due, nelle altre colonne, e può continuare nella pagina dopo.</para>
/// <para><b>Le attese</b> sono una tabella di 6 colonne: punto · rilevamenti e distanze · MHA/MHL · virata · rotta
/// d'avvicinamento · note. Ogni terna quota-virata-rotta è un'attesa; un punto può averne più d'una.</para>
/// </summary>
public static partial class ItalianEnr3
{
    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    [GeneratedRegex(@"^(?<s>[▲∆△▴])\s*(?<nome>.+)$", Opzioni)]
    private static partial Regex RxPunto();

    [GeneratedRegex(@"['’‘`]{1,2}(?<id>[A-Z0-9]{2,4})['’‘`]{1,2}", Opzioni)]
    private static partial Regex RxSigla();

    [GeneratedRegex(@"\d{3}\s*°\s*/\s*\d{3}\s*°", Opzioni)]
    private static partial Regex RxRotta();

    [GeneratedRegex(@"^\d+(?:[.,]\d+)?$", Opzioni)]
    private static partial Regex RxNumero();

    [GeneratedRegex(@"FL\s*\d{2,3}|UNL|GND|SFC|\d[\d.,]*\s*(?:ft|FT|m|M)(?:\s*(?:AMSL|AGL|MSL|SFC|GND))?", Opzioni)]
    private static partial Regex RxLimite();

    [GeneratedRegex(@"^\(RNAV\s*[\d.]+\)$|^\(RNP\s*[\d.]+\)$", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxNavigazione();

    // I rimandi alle note anche sulla stessa riga della quota: «5000 (1)» (CHI in ENR 3.4.2).
    [GeneratedRegex(@"^(?<q>FL\s*\d{2,3}|\d{3,5}(?:\s*ft)?|TRL)(?:\s+only)?(?:\s*(?<r>\(\d+\)(?:\s*\(\d+\))*))?$", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxQuotaDiAttesa();

    [GeneratedRegex(@"^or(?:\s+TRL)?$", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxSeguitoDellaQuota();

    [GeneratedRegex(@"^(?<g>\d{3})\s*°$", Opzioni)]
    private static partial Regex RxGradi();

    [GeneratedRegex(@"^\((?:\d+)\)(?:\s*\(\d+\))*$", Opzioni)]
    private static partial Regex RxRimandi();

    [GeneratedRegex(@"^[A-Z]{2,5}$", Opzioni)]
    private static partial Regex RxFix();

    [GeneratedRegex(@"^CDR\s*\d$", Opzioni)]
    private static partial Regex RxCdr();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    // --- le rotte ----------------------------------------------------------------------------------------------------

    private sealed class PuntoLetto
    {
        public required string Testo { get; set; }
        public required bool Obbligatorio { get; init; }
        public required double Y { get; init; }
        public required int Pagina { get; init; }
        public Position? Posizione { get; set; }
        public List<(double Y, string Testo)> Note { get; } = [];
    }

    /// <summary>Le righe di testo del tratto, colonna per colonna, nell'ordine in cui si leggono (anche su due pagine).</summary>
    private sealed class TrattoLetto
    {
        public List<string>[] Colonne { get; } = [.. Enumerable.Range(0, 7).Select(_ => new List<string>())];
        public bool Vuoto => Colonne.Skip(1).All(c => c.Count == 0);
    }

    /// <summary>Una rotta per file: le pagine di un PDF di ENR 3.1-3.3, nel modello.</summary>
    public static ImportResult ReadRoutes(IReadOnlyList<PdfPageText> pages, string section)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var pagine = new List<ImportPage>();
        var problemi = new List<ImportIssue>();
        var punti = new List<PuntoLetto>();
        var tratti = new List<TrattoLetto>();
        var testa = new List<string>();
        AipPage? primaPagina = null;

        foreach (var page in pages)
        {
            var piane = Table.PlainLines(page);
            var riferimento = PageInfo.Reference(piane);
            var data = PageInfo.Date(piane);
            var griglia = Griglia(page, 7);
            pagine.Add(new ImportPage(page.Number, riferimento, data, griglia is not null));
            if (griglia is null) continue;
            if (data is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));
            primaPagina ??= riferimento is null ? null : new AipPage(riferimento, data);

            var righe = Table.Lines(page, griglia);
            // Sotto la riga dei numeri delle colonne («1 2 3 4 5 6»): sopra c'è la testata.
            var numeri = righe.FirstOrDefault(r => r.Column == 0 && r.Text.Trim() == "1");
            var dati = righe.Where(r => numeri is null || r.Y > numeri.Y + 2).OrderBy(r => r.Y).ThenBy(r => r.Column).ToList();

            // Dove comincia e dove finisce la parte del punto (il nome e le coordinate): il resto è del tratto.
            foreach (var r in dati)
            {
                var testo = r.Text.Trim();
                if (r.Column == 0)
                {
                    if (RxPunto().Match(testo) is { Success: true } m)
                    {
                        punti.Add(new PuntoLetto { Testo = m.Groups["nome"].Value.Trim(), Obbligatorio = m.Groups["s"].Value == "▲", Y = r.Y, Pagina = page.Number });
                        tratti.Add(new TrattoLetto());
                        continue;
                    }
                    if (punti.Count == 0)
                    {
                        testa.Add(testo);
                        continue;
                    }
                    var ultimo = punti[^1];
                    if (ultimo.Posizione is null && GeometryText.ReadPoint(testo) is { } p)
                        ultimo.Posizione = p;
                    else if (ultimo.Posizione is null)
                        ultimo.Testo += " " + testo;  // il nome su due righe: «CARAFFA DI CATANZARO» / «VORTAC 'CDC'»
                    else if (RxCdr().IsMatch(testo))
                        tratti[^1].Colonne[6].Insert(0, testo);  // la categoria della rotta condizionale è del tratto dopo
                    else if (testo.Replace("\\", "", StringComparison.Ordinal).Trim().Length == 0)
                        tratti[^1].Colonne[1].Add(testo);  // la riga di barre dell'interruzione comincia qui
                    else
                        ultimo.Note.Add((r.Y, testo));
                    continue;
                }

                // Una colonna di dati: col punto finché le sue coordinate non sono lette (le note accanto al nome,
                // «For continuation see AIP AUSTRIA»), poi col tratto che parte da lui — anche nella pagina dopo.
                if (punti.Count == 0)
                {
                    problemi.Add(new ImportIssue(ImportIssueKind.Route, null, page.Number, $"prima del primo punto: {testo}"));
                    continue;
                }
                var punto = punti[^1];
                if (punto.Posizione is null)
                {
                    // La nota del punto è una cella unita sopra le colonne 2-6: i suoi pezzi, in ordine, sono una frase.
                    punto.Note.Add((r.Y, testo));
                    continue;
                }
                tratti[^1].Colonne[r.Column].Add(testo);
            }
        }

        var (designatore, navigazione) = Testa(testa, problemi);
        var puntiDelModello = new List<RoutePoint>();
        foreach (var p in punti)
        {
            if (p.Posizione is not { } posizione)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.Route, designatore, p.Pagina, $"{p.Testo}: senza coordinate"));
                continue;
            }
            var sigla = RxSigla().Match(p.Testo) is { Success: true } s ? s.Groups["id"].Value : p.Testo;
            puntiDelModello.Add(new RoutePoint(sigla, p.Testo, posizione, p.Obbligatorio,
                p.Note.Count == 0 ? null : Righe(p.Note)));
        }

        if (puntiDelModello.Count < 2 || puntiDelModello.Count != punti.Count || designatore is null)
        {
            problemi.Add(new ImportIssue(ImportIssueKind.Route, designatore, null, $"{puntiDelModello.Count} punti: la rotta non entra nel modello"));
            return new ImportResult([], pagine, problemi);
        }

        // I tratti: uno per punto, quello dell'ultimo vuoto. Dopo l'ultimo punto possono stare solo le note (M740: «For
        // continuation see AIP MALTA»): sono della rotta. Altri dati lì si dicono.
        string? noteDellaRotta = null;
        if (!tratti[^1].Vuoto)
        {
            // La nota può essere una cella unita su più colonne: conta che non ci sia nessun dato di tratto.
            var dopo = tratti[^1].Colonne.Skip(1).SelectMany(c => c).ToList();
            if (dopo.All(r => !RxRotta().IsMatch(r) && !RxLimite().IsMatch(r) && !RxNumero().IsMatch(r.Trim()) && !r.Contains('↓', StringComparison.Ordinal) && !r.Contains('↑', StringComparison.Ordinal)))
                noteDellaRotta = RxSpazi().Replace(string.Join(" ", dopo), " ").Trim();
            else
                problemi.Add(new ImportIssue(ImportIssueKind.Route, designatore, null, "dati dopo l'ultimo punto"));
        }
        var trattiDelModello = new List<RouteSegment>();
        for (var i = 0; i + 1 < punti.Count; i++)
        {
            if (tratti[i].Vuoto)
                problemi.Add(new ImportIssue(ImportIssueKind.Route, designatore, punti[i].Pagina, $"{punti[i].Testo} → {punti[i + 1].Testo}: tratto senza dati"));
            trattiDelModello.Add(Tratto(tratti[i], designatore, punti[i].Testo, problemi, punti[i].Pagina));
        }

        // ENR 3.2-1, nota 3: «in mancanza di dati … dovranno ritenersi validi i valori precedenti» (carta «L'AIP intero»,
        // passo 2d). Si ereditano i limiti verticali e l'ampiezza dal tratto prima; rotta, distanza e livelli minimi no
        // (sono del tratto e del verso). Senza un tratto prima, i limiti che mancano si dicono.
        for (var i = 0; i < trattiDelModello.Count; i++)
        {
            var t = trattiDelModello[i];
            if (t.Discontinuity) continue;
            var prima = trattiDelModello.Take(i).LastOrDefault(s => !s.Discontinuity);
            if (prima is not null)
                trattiDelModello[i] = t with
                {
                    Upper = t.Upper ?? prima.Upper,
                    Lower = t.Lower ?? prima.Lower,
                    LateralLimits = t.LateralLimits ?? prima.LateralLimits,
                };
            else if (t.Upper is null && t.Lower is null)
                problemi.Add(new ImportIssue(ImportIssueKind.Route, designatore, punti[i].Pagina, $"{punti[i].Testo}: senza limiti verticali e senza un tratto prima"));
        }

        var rotta = new Route
        {
            Section = section,
            Designator = designatore,
            Navigation = navigazione,
            Points = puntiDelModello,
            Segments = trattiDelModello,
            Page = primaPagina,
            Remarks = noteDellaRotta,
        };
        return new ImportResult([], pagine, problemi) { Routes = [rotta] };
    }

    /// <summary>I pezzi di testo a righe: quelli alla stessa altezza uniti da uno spazio (una cella unita su più colonne).</summary>
    private static string Righe(List<(double Y, string Testo)> pezzi)
    {
        var righe = new List<List<(double Y, string Testo)>>();
        foreach (var p in pezzi)
        {
            if (righe.Count > 0 && Math.Abs(p.Y - righe[^1][0].Y) <= 3) righe[^1].Add(p);
            else righe.Add([p]);
        }
        return string.Join("\n", righe.Select(r => string.Join(" ", r.Select(x => x.Testo))));
    }

    /// <summary>L'identificativo e la specifica di navigazione, dal testo della prima colonna sopra il primo punto.</summary>
    private static (string? Designatore, string? Navigazione) Testa(List<string> testa, List<ImportIssue> problemi)
    {
        string? designatore = null;
        string? navigazione = null;
        foreach (var t in testa)
        {
            if (RxNavigazione().IsMatch(t)) navigazione = t.Trim('(', ')').Trim();
            else if (designatore is null && Regex.IsMatch(t, @"^[A-Z]{1,2}\d{1,4}$", RegexOptions.CultureInvariant)) designatore = t;
            else problemi.Add(new ImportIssue(ImportIssueKind.Route, designatore, null, $"prima del primo punto: {t}"));
        }
        return (designatore, navigazione);
    }

    private static RouteSegment Tratto(TrattoLetto t, string rotta, string da, List<ImportIssue> problemi, int pagina)
    {
        // Una riga di barre rovesciate su tutta la larghezza: la rotta si interrompe (Z910 fra IPKAM e CDC).
        if (!t.Vuoto && t.Colonne.Skip(1).SelectMany(c => c).All(r => r.Replace("\\", "", StringComparison.Ordinal).Trim().Length == 0))
            return new RouteSegment { Discontinuity = true };

        string? traccia = null;
        double? distanza = null;
        foreach (var riga in t.Colonne[1])
        {
            if (RxRotta().Match(riga) is { Success: true } m) traccia = RxSpazi().Replace(m.Value, " ");
            else if (RxNumero().IsMatch(riga)) distanza = double.Parse(riga.Replace(',', '.'), CultureInfo.InvariantCulture);
            else problemi.Add(new ImportIssue(ImportIssueKind.Route, rotta, pagina, $"{da}: rotta e distanza «{riga}»"));
        }

        var limiti = RxLimite().Matches(string.Join(" ", t.Colonne[2])).Select(m => RxSpazi().Replace(m.Value, " ")).ToList();
        // Nessun limite: vale quello del tratto prima (nota 3); uno solo, o più di due, si dice.
        if (limiti.Count is 1 or > 2)
            problemi.Add(new ImportIssue(ImportIssueKind.Route, rotta, pagina, $"{da}: {limiti.Count} limiti verticali «{string.Join(" ", t.Colonne[2])}»"));

        var (dispari, minimaDispari) = Livelli(t.Colonne[4]);
        var (pari, minimaPari) = Livelli(t.Colonne[5]);
        return new RouteSegment
        {
            Track = traccia,
            DistanceNm = distanza,
            Upper = limiti.Count > 0 ? LimitText.Parse(limiti[0]) : null,
            Lower = limiti.Count > 1 ? LimitText.Parse(limiti[1]) : null,
            LateralLimits = t.Colonne[3].Count == 0 ? null : string.Join(" ", t.Colonne[3]),
            Odd = dispari,
            Even = pari,
            MinimumOdd = minimaDispari,
            MinimumEven = minimaPari,
            Remarks = t.Colonne[6].Count == 0 ? null : string.Join(" ", t.Colonne[6]),
        };
    }

    /// <summary>Una colonna dei livelli: la freccia (↓ avanti, ↑ indietro) e la quota minima.</summary>
    private static (RouteDirection Verso, string? Minima) Livelli(List<string> colonna)
    {
        var testo = string.Join(" ", colonna);
        var verso = testo.Contains('↓', StringComparison.Ordinal) ? RouteDirection.Forward
            : testo.Contains('↑', StringComparison.Ordinal) ? RouteDirection.Backward
            : RouteDirection.None;
        var minima = testo.Replace("↓", "", StringComparison.Ordinal).Replace("↑", "", StringComparison.Ordinal).Trim();
        return (verso, minima.Length == 0 ? null : RxSpazi().Replace(minima, " "));
    }

    // --- le attese ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Le colonne di una tabella di attese: ENR 3.4.1 (punto · riferimento · quota · virata · rotta · note) ed ENR 3.4.2
    /// (radioassistenza · quota · virata · rotta · rotte e procedure interessate · note).
    /// </summary>
    private sealed record ColonneDelleAttese(string Sezione, int? Riferimento, int Quota, int Virata, int Rotta, int Note, int? Interessate);

    private static readonly ColonneDelleAttese Enr341 = new("ENR 3.4.1", 1, 2, 3, 4, 5, null);

    private static readonly ColonneDelleAttese Enr342 = new("ENR 3.4.2", null, 1, 2, 3, 5, 4);

    /// <summary>Le attese in rotta di ENR 3.4.1, nel modello.</summary>
    public static ImportResult ReadHoldings(IReadOnlyList<PdfPageText> pages) => Attese(pages, Enr341);

    /// <summary>
    /// Le attese sulle radioassistenze di ENR 3.4.2 (carta F6, «finiamo con l'importer»): la stessa tabella delle attese con
    /// le colonne in un altro ordine. Nella prima colonna la sigla col tipo (<c>AEA VOR/DME</c>) e sotto il nome fra parentesi
    /// (<c>(Alghero)</c>), che si salta; le rotte e procedure interessate vanno con tutte le attese della radioassistenza.
    /// Una radioassistenza senza attese (AHO TACAN) non dà niente.
    /// </summary>
    public static ImportResult ReadNavaidHoldings(IReadOnlyList<PdfPageText> pages) => Attese(pages, Enr342);

    private static ImportResult Attese(IReadOnlyList<PdfPageText> pages, ColonneDelleAttese c)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var pagine = new List<ImportPage>();
        var problemi = new List<ImportIssue>();
        var attese = new List<Holding>();
        string? fix = null;
        string? tipo = null;
        var note = new List<string>();
        var interessate = new List<string>();
        var delFix = new List<Holding>();
        // ENR 3.4.2: ogni radioassistenza dell'elenco, anche senza attese (AHO TACAN), col nome fra parentesi.
        var elenco = new List<NavaidProcedures>();
        string? nomeDellaStazione = null;
        AipPage? paginaDelFix = null;

        void Chiudi()
        {
            var testo = note.Count == 0 ? null : string.Join("\n", note);
            var rotte = interessate.Count == 0 ? null : string.Join("\n", interessate);
            attese.AddRange(delFix.Select(a => a with { Remarks = testo, Affected = rotte }));
            if (c.Interessate is not null && fix is not null)
                elenco.Add(new NavaidProcedures
                {
                    Section = c.Sezione, Designator = fix, Type = tipo, Name = nomeDellaStazione, Affected = rotte, Page = paginaDelFix,
                });
            nomeDellaStazione = null;
            delFix.Clear();
            note.Clear();
            interessate.Clear();
        }

        foreach (var page in pages)
        {
            var piane = Table.PlainLines(page);
            var riferimento = PageInfo.Reference(piane);
            var data = PageInfo.Date(piane);
            var griglia = Griglia(page, 6);
            pagine.Add(new ImportPage(page.Number, riferimento, data, griglia is not null));
            if (griglia is null)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutTable, null, page.Number));
                continue;
            }
            if (data is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));
            var pagina = riferimento is null ? null : new AipPage(riferimento, data);

            var righe = Table.Lines(page, griglia);
            var numeri = righe.FirstOrDefault(r => r.Column == 0 && r.Text.Trim() == "1");
            var dati = righe.Where(r => numeri is null || r.Y > numeri.Y + 2).ToList();
            var usate = new HashSet<TextLine>();
            // Dove comincia ogni punto: virata e rotta si cercano solo fra le righe del suo punto (KALIK: un'attesa del
            // punto prima, senza virata accanto, prendeva la sua).
            var inizi = dati.Where(r => r.Column == 0 && RxFix().IsMatch(r.Text.Trim().Split(' ')[0])).Select(r => r.Y).Order().ToList();

            // 🔴 Le fasce della tabella (carta «L'AIP intero», passo 2a): dove la pagina ha i bordi orizzontali, il punto è la
            // sua fascia, e il nome sta in mezzo alla cella — le righe sopra il nome sono sue (TUCOZ nel 2611: la quota 3000
            // e le note 1-2 stavano 30 punti sopra il nome, e finivano a TOPRU). In una fascia con un nome solo il nome si
            // legge per primo, e virata e rotta si cercano nella fascia; una fascia con una quota, una virata e una rotta le
            // abbina anche lontane (TUCOZ: 29 punti fra la quota e la virata).
            var bande = Table.RowBands(page, griglia);
            int Banda(double y)
            {
                for (var i = 0; i < bande.Count; i++)
                    if (y >= bande[i].Top - 1 && y <= bande[i].Bottom + 1) return i;
                return -1;
            }
            bool UnNome(TextLine r) => r.Column == 0 && !(c.Interessate is not null && r.Text.Trim().StartsWith('('))
                                       && RxFix().IsMatch(r.Text.Trim().Split(' ')[0]);
            var nomiPerBanda = dati.Where(UnNome).GroupBy(r => Banda(r.Y)).ToDictionary(g => g.Key, g => g.Count());
            bool ConUnNome(int b) => b >= 0 && nomiPerBanda.GetValueOrDefault(b) == 1;
            var abbinate = new Dictionary<TextLine, (TextLine Virata, TextLine Rotta)>();
            foreach (var b in Enumerable.Range(0, bande.Count).Where(b => ConUnNome(b) || !nomiPerBanda.ContainsKey(b)))
            {
                var nella = dati.Where(r => Banda(r.Y) == b).OrderBy(r => r.Y).ToList();
                var quote = nella.Where(r => r.Column == c.Quota && RxQuotaDiAttesa().IsMatch(r.Text.Trim())).ToList();
                var virate = nella.Where(r => r.Column == c.Virata && r.Text.Trim() is "R" or "L").ToList();
                var rotte = nella.Where(r => r.Column == c.Rotta && RxGradi().IsMatch(r.Text.Trim())).ToList();
                // Solo una per tipo: con più quote l'ordine non basta (JESSY, 2611: la seconda 4000 sta accanto a 033°, non
                // alla seconda 034°) e si resta al più vicino, dicendo quel che avanza.
                if (quote.Count == 1 && virate.Count == 1 && rotte.Count == 1)
                    abbinate[quote[0]] = (virate[0], rotte[0]);
            }

            // Le terne quota-virata-rotta: le righe della colonna 2 con una quota, e accanto virata e rotta. ⚠️ Non sempre
            // sulla stessa riga: nelle attese di un punto con due riferimenti la rotta sta 8 punti più su (BABIK), la virata
            // anche 20 più giù (IRZEC). Si prende la più vicina non ancora usata, entro 25 punti.
            foreach (var r in dati
                         .OrderBy(r => ConUnNome(Banda(r.Y)) ? bande[Banda(r.Y)].Top : r.Y)
                         .ThenBy(r => ConUnNome(Banda(r.Y)) && UnNome(r) ? 0 : 1)
                         .ThenBy(r => r.Y).ThenBy(r => r.Column))
            {
                var testo = r.Text.Trim();
                var colonna = r.Column;
                if (colonna == 0)
                {
                    // ENR 3.4.2: il nome della stazione sotto la sigla, «(Alghero)».
                    if (c.Interessate is not null && testo.StartsWith('('))
                    {
                        nomeDellaStazione = testo.Trim('(', ')', ' ');
                        continue;
                    }
                    var parola = testo.Split(' ')[0];
                    if (!RxFix().IsMatch(parola))
                    {
                        problemi.Add(new ImportIssue(ImportIssueKind.Holding, fix, page.Number, testo));
                        continue;
                    }
                    Chiudi();
                    fix = parola;
                    paginaDelFix = pagina;
                    tipo = c.Interessate is null ? null : SenzaVuoto(testo[parola.Length..].Trim());
                    continue;
                }
                if (colonna == c.Quota)
                {
                    if (RxRimandi().IsMatch(testo))
                    {
                        if (delFix.Count > 0) delFix[^1] = delFix[^1] with { NoteRefs = RxSpazi().Replace(testo, "") };
                        continue;
                    }
                    if (delFix.Count > 0 && (RxSeguitoDellaQuota().IsMatch(testo)
                                             || testo == "TRL" && delFix[^1].Minimum.Text.EndsWith(" or", StringComparison.Ordinal)))
                    {
                        // «FL70» / «or» / «TRL» (IPKOV): la quota continua sotto.
                        delFix[^1] = delFix[^1] with { Minimum = delFix[^1].Minimum with { Text = delFix[^1].Minimum.Text + " " + testo } };
                        continue;
                    }
                    if (fix is null || RxQuotaDiAttesa().Match(testo) is not { Success: true } quota)
                    {
                        problemi.Add(new ImportIssue(ImportIssueKind.Holding, fix, page.Number, $"quota «{testo}»"));
                        continue;
                    }
                    string? virata, rotta;
                    if (abbinate.TryGetValue(r, out var coppia) && !usate.Contains(coppia.Virata) && !usate.Contains(coppia.Rotta))
                    {
                        usate.Add(coppia.Virata);
                        usate.Add(coppia.Rotta);
                        (virata, rotta) = (coppia.Virata.Text.Trim(), coppia.Rotta.Text.Trim());
                    }
                    else
                    {
                        var banda = Banda(r.Y);
                        var da = ConUnNome(banda) ? bande[banda].Top - 1 : inizi.LastOrDefault(y => y <= r.Y + 3, double.MinValue) - 3;
                        var a = ConUnNome(banda) ? bande[banda].Bottom + 1 : inizi.FirstOrDefault(y => y > da + 6, double.MaxValue) - 3;
                        virata = Vicina(dati, usate, c.Virata, r.Y, da, a);
                        rotta = Vicina(dati, usate, c.Rotta, r.Y, da, a);
                    }
                    if (virata is not ("R" or "L") || rotta is null || RxGradi().Match(rotta) is not { Success: true } g)
                    {
                        problemi.Add(new ImportIssue(ImportIssueKind.Holding, fix, page.Number, $"{testo} {virata} {rotta}"));
                        continue;
                    }
                    delFix.Add(new Holding
                    {
                        Section = c.Sezione,
                        Fix = fix,
                        FixType = tipo,
                        // Il riferimento accanto alla quota; in una fascia con una quota sola, se accanto non c'è, quello
                        // della fascia (TUCOZ: la quota 29 punti sopra il riferimento). Con più quote no: un punto può avere
                        // due riferimenti, ognuno con le sue attese (BABIK).
                        Bearing = c.Riferimento is { } rif
                            ? Accanto(dati, rif, r.Y, 9) ?? (abbinate.ContainsKey(r)
                                ? dati.Where(d => d.Column == rif && Banda(d.Y) == Banda(r.Y)).MinBy(d => d.Y)?.Text.Trim()
                                : null)
                            : null,
                        // «5000 only» (BAVOM): la quota è il numero, il testo resta com'è.
                        Minimum = QuotaDiAttesa(quota.Groups["q"].Value) with { Text = quota.Groups["r"].Success ? testo[..quota.Groups["r"].Index].Trim() : testo },
                        NoteRefs = quota.Groups["r"].Success ? RxSpazi().Replace(quota.Groups["r"].Value, "") : null,
                        Turn = virata == "R" ? TurnDirection.Right : TurnDirection.Left,
                        InboundTrack = int.Parse(g.Groups["g"].Value, CultureInfo.InvariantCulture),
                        Page = pagina,
                    });
                    continue;
                }
                if (colonna == c.Note && fix is not null) note.Add(testo);
                else if (colonna == c.Interessate && fix is not null) interessate.Add(testo);
            }

            // Una virata o una rotta che nessuna quota ha preso: un'attesa che non si sa leggere, si dice.
            foreach (var r in dati.Where(r => (r.Column == c.Virata || r.Column == c.Rotta) && !usate.Contains(r)))
            {
                var suo = dati.Where(d => d.Column == 0 && d.Y <= r.Y + 3).MaxBy(d => d.Y)?.Text.Split(' ')[0];
                problemi.Add(new ImportIssue(ImportIssueKind.Holding, suo, page.Number, $"senza quota: {r.Text.Trim()}"));
            }
        }
        Chiudi();
        return new ImportResult([], pagine, problemi) { Holdings = attese, NavaidProcedures = elenco };
    }

    /// <summary>
    /// Le colonne della tabella dalle x di <b>tutte</b> le righe verticali della pagina, dalla testata in giù. Non
    /// <see cref="Table.FindAll"/>: nelle rotte di due punti le righe dei punti sono celle unite su tutta la larghezza, e le
    /// righe interne non coprono metà della banda (T543, misura del 18 ottobre). Una barra di modifica a 4-5 punti dal bordo
    /// sinistro non è una colonna. Null se le colonne non sono <paramref name="colonne"/> (una pagina bianca).
    /// </summary>
    internal static TableGrid? Griglia(PdfPageText page, int colonne)
    {
        var lunghi = page.VerticalRules.Where(r => r.Bottom - r.Top >= 10).ToList();
        if (lunghi.Count == 0) return null;
        var xs = new List<List<double>>();
        foreach (var x in lunghi.Select(r => r.X).Order())
        {
            if (xs.Count > 0 && x - xs[^1][^1] <= 2.5) xs[^1].Add(x);
            else xs.Add([x]);
        }
        var colonneX = xs.Select(g => Math.Round(g.Average(), 1)).ToList();
        if (colonneX.Count > 1 && colonneX[1] - colonneX[0] < 6) colonneX.RemoveAt(0);
        return colonneX.Count == colonne + 1
            ? new TableGrid(colonneX, lunghi.Min(r => r.Top), lunghi.Max(r => r.Bottom), Inherited: false, Cut: true)
            : null;
    }

    private static string? SenzaVuoto(string t) => t.Length == 0 ? null : t;

    /// <summary>
    /// La quota di un'attesa. Un numero senza unità («3000», 481 attese su 542 nel 2611) è in piedi sul livello del mare:
    /// la colonna MHA/MHL è in piedi e ENR 3.2 dice che le quote in piedi sono AMSL (committente, 21 ottobre). Il testo
    /// resta com'è stampato.
    /// </summary>
    private static VerticalLimit QuotaDiAttesa(string quota)
    {
        var letta = LimitText.Parse(quota);
        return letta.Reference == VerticalReference.Unknown
               && int.TryParse(quota.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var piedi)
            ? letta with { Reference = VerticalReference.Msl, Value = piedi, Unit = VerticalUnit.Ft }
            : letta;
    }

    /// <summary>La riga di quella colonna più vicina in altezza (entro 25 punti) e non ancora presa da un'altra attesa.</summary>
    private static string? Vicina(List<TextLine> righe, HashSet<TextLine> usate, int colonna, double y, double da, double a)
    {
        var scelta = righe.Where(r => r.Column == colonna && !usate.Contains(r) && Math.Abs(r.Y - y) <= 25 && r.Y >= da && r.Y < a)
            .MinBy(r => Math.Abs(r.Y - y));
        if (scelta is null) return null;
        usate.Add(scelta);
        return scelta.Text.Trim();
    }

    /// <summary>Il testo di quella colonna sulla stessa riga (entro <paramref name="tolleranza"/> punti), o null.</summary>
    private static string? Accanto(List<TextLine> righe, int colonna, double y, double tolleranza = 3) =>
        righe.Where(r => r.Column == colonna && Math.Abs(r.Y - y) <= tolleranza).MinBy(r => Math.Abs(r.Y - y))?.Text.Trim();
}
