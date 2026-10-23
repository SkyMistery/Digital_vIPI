using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Il testo di AD 2 di uno scalo (carta F6, slice 5a): il primo PDF di ogni scalo (<c>LI-AD 2 LICA - LAMEZIA TERME 1</c>),
/// le sezioni AD 2.1-2.24 una dopo l'altra, ognuna con le sue tabelle. Si leggono:
/// <list type="bullet">
/// <item><b>AD 2.2</b>, una tabella numerata a tre colonne: ARP (1), elevazione e temperatura (3), declinazione e
/// variazione annuale (5), traffico (7).</item>
/// <item><b>AD 2.12</b>, le piste: tre tabelle (colonne 1-6, 1 e 7-11, 1 e 12-13: le riconosce la riga dei numeri), una
/// riga per designatore; una riga senza designatore continua quella sopra (la fine della pista nella pagina dopo).</item>
/// <item><b>AD 2.13</b>, le distanze dichiarate: la pista e, sotto, le intersezioni, una per riga di testo.</item>
/// <item><b>AD 2.17</b>, lo spazio aereo dello scalo (ATZ, CTR): designatore e limiti laterali, limiti verticali, classe,
/// nominativo e lingua, altitudine di transizione, note — nel modello come un'area della sezione <c>AD 2.17</c>.</item>
/// <item><b>AD 2.18</b>, i servizi di comunicazione: servizio, nominativo, frequenza, orario, note.</item>
/// <item><b>AD 2.19</b>, le radioassistenze (slice 5d): 8 colonne — tipo e declinazione · sigla · frequenza e canale ·
/// orario per apparato · coordinate per apparato · elevazione del DME · copertura · note —, una riga per radioassistenza o
/// per apparato di un ILS. La riga «ILS RWY 28 LOC» apre un ILS; le righe sotto (GP, OM, MM senza sigla, il DME con la
/// sigla del localizzatore) sono i suoi apparati; il resto è una radioassistenza come quelle di ENR 4.1.</item>
/// </list>
/// Una tabella appartiene alla sezione dell'ultimo titolo «LICA AD 2.N» sopra di lei, anche nella pagina prima.
/// </summary>
public static partial class ItalianAd2
{
    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    [GeneratedRegex(@"^(?<icao>[A-Z]{4})\s+AD\s+[23]\.(?<n>\d+)\b", Opzioni)]
    private static partial Regex RxTitolo();

    [GeneratedRegex(@"(['’""″])\s+(?=[NSEW]\b)")]
    private static partial Regex RxSpazioPrimaDellEmisfero();

    // Una riga che continua il nome dello spazio in AD 2.17: solo il tipo, col settore («ATZ Sector 3», «Airport ATZ»).
    [GeneratedRegex(@"^(?:Airport\s+)?(?:ATZ|CTR|RMZ|TMZ)(?:\s+Sector\s+\w+)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxSeguitoDelNome();

    [GeneratedRegex(@"([NS])\s*-\s*(?=\d)")]
    private static partial Regex RxTrattinoFraLeCoordinate();

    // Il nome dell'eliporto, sotto il titolo di AD 3.1: «LIBI - Vieste».
    [GeneratedRegex(@"^(?<icao>[A-Z]{4})\s*-\s*(?<nome>\S.*)$", Opzioni)]
    private static partial Regex RxNomeDelloScalo();

    /// <summary>
    /// I numeri delle sezioni che hanno un lettore loro, in AD 2 (gli scali) e in AD 3 (gli eliporti, carta «L'AIP intero»,
    /// passo 5: niente piste, lo spazio aereo in 3.16, le frequenze in 3.17, le radioassistenze in 3.18), e i numeri delle voci
    /// di 2.2/3.2 che si leggono: ARP, elevazione, declinazione, traffico.
    /// </summary>
    private sealed record Parte(int Numero, int? Piste, int? Distanze, int Spazi, int Frequenze, int Radio, string Arp, string Elevazione,
        string Declinazione, string Traffico);

    private static readonly Parte Ad2 = new(2, 12, 13, 17, 18, 19, "1", "3", "5", "7");
    private static readonly Parte Ad3 = new(3, null, null, 16, 17, 18, "1", "3", "4", "6");

    [GeneratedRegex(@"\d+(?:[.,]\d+)?\s*FT\b", Opzioni)]
    private static partial Regex RxPiedi();

    [GeneratedRegex(@"^\d+(?:[.,]\d+)?\s*MHZ$|^\d{3}\.\d{2,3}$", Opzioni)]
    private static partial Regex RxFrequenza();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    // «2000FT», «FL110», «TRL»; quattro carte su cinque senza «FT» («2500»); «3000/4000 (5)» a LIRF; «FL 70/TRL» a LIRA
    // (segnalazioni 2611, B8).
    [GeneratedRegex(@"^(?<q>(?:\d{3,5}(?:\s*FT)?|FL\s*\d{2,3}|TRL)(?:\s*/\s*(?:\d{3,5}(?:\s*FT)?|FL\s*\d{2,3}|TRL))?)(?:\s*(?<r>\(\d+\)(?:\s*\(\d+\))*))?(?:\s+(?<n>.*))?$", Opzioni)]
    private static partial Regex RxQuotaMinima();

    [GeneratedRegex(@"^(?:REMARKS?|NOTE|NOTA)\b", Opzioni)]
    private static partial Regex RxNotaDellaCarta();

    [GeneratedRegex(@"\s(?=\d{1,2}[.)]\s?\D)")]
    private static partial Regex RxNumeroDellaNota();

    [GeneratedRegex(@"^ILS\s+RWY\s+(?<pista>\d{2}[LRC]?)\s+LOC(?:\s+(?<cat>CAT\s+I{1,3}[ABC]?))?\b", Opzioni)]
    private static partial Regex RxIls();

    [GeneratedRegex(@"\s*\((?<v>[^()]*\d[^()]*)\)\s*$", Opzioni)]
    private static partial Regex RxDeclinazioneInFondo();

    [GeneratedRegex(@"^(?<a>[A-Z][A-Z/-]*)\s+(?<h>H24|HJ|HN|HX|HO|\d.*)$", Opzioni)]
    private static partial Regex RxOrarioDiUnApparato();

    [GeneratedRegex(@"\bSlope\s+(?<v>\d+(?:\.\d+)?\s*[°º])", Opzioni | RegexOptions.IgnoreCase)]
    private static partial Regex RxPendenza();

    [GeneratedRegex(@"\bRDH:?\s*(?<v>\d+(?:\.\d+)?\s*M)\b", Opzioni)]
    private static partial Regex RxRdh();

    /// <summary>Gli apparati che seguono il localizzatore di un ILS, senza sigla o con la sua.</summary>
    private static readonly string[] ApparatiDellIls = ["GP", "DME", "DME-P", "OM", "MM", "IM", "LOC"];

    /// <summary>Una riga di una tabella: il testo di ogni colonna (una voce per riga di testo), la pagina, la riga dei numeri della tabella.</summary>
    private sealed record Riga(List<string>[] Celle, ImportPage Pagina, string Numeri);

    public static ImportResult Read(IReadOnlyList<PdfPageText> pages, string icao, string nome) => Leggi(pages, icao, nome, Ad2);

    /// <summary>Il testo di un eliporto di AD 3 (<c>LI-AD 3 LIBI 1</c>): il nome sotto il titolo di AD 3.1.</summary>
    public static ImportResult ReadHeliport(IReadOnlyList<PdfPageText> pages, string icao) => Leggi(pages, icao, null, Ad3);

    private static ImportResult Leggi(IReadOnlyList<PdfPageText> pages, string icao, string? nome, Parte parte)
    {
        ArgumentNullException.ThrowIfNull(pages);
        nome ??= pages.SelectMany(Table.PlainLines).Select(l => RxNomeDelloScalo().Match(l.Trim()))
            .FirstOrDefault(m => m.Success && m.Groups["icao"].Value == icao)?.Groups["nome"].Value.Trim() ?? icao;
        var problemi = new List<ImportIssue>();
        var pagine = new List<ImportPage>();
        var perSezione = new Dictionary<int, List<Riga>>();
        var ultimaGriglia = new Dictionary<int, TableGrid>();
        var sezione = 0;
        foreach (var page in pages)
        {
            var piane = Table.PlainLinesWithY(page);
            var testo = piane.Select(l => l.Text).ToList();
            var data = PageInfo.Date(testo);
            var griglie = Table.FindAll(page, 300).OrderBy(g => g.Top).ToList();
            var pagina = new ImportPage(page.Number, PageInfo.Reference(testo), data, griglie.Count > 0);
            pagine.Add(pagina);
            if (data is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));
            var titoli = piane.Select(l => (l.Y, M: RxTitolo().Match(l.Text.Trim())))
                .Where(x => x.M.Success && x.M.Groups["icao"].Value == icao)
                .Select(x => (x.Y, N: int.Parse(x.M.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture))).ToList();
            foreach (var g in griglie)
            {
                var sua = titoli.Where(t => t.Y < g.Top).Select(t => (int?)t.N).LastOrDefault() ?? sezione;
                if (!perSezione.TryGetValue(sua, out var righe)) perSezione[sua] = righe = [];
                righe.AddRange(Righe(page, Completa(g, ultimaGriglia.GetValueOrDefault(sua)), pagina));
                ultimaGriglia[sua] = g.ColumnCount >= (ultimaGriglia.GetValueOrDefault(sua)?.ColumnCount ?? 0) ? g : ultimaGriglia[sua];
            }
            if (titoli.Count > 0) sezione = titoli[^1].N;
        }

        var aree = new List<Airspace>();
        var scalo = new Aerodrome
        {
            Icao = icao,
            Name = nome,
            Heliport = parte == Ad3,
        };
        if (perSezione.TryGetValue(2, out var r22)) scalo = Dati(scalo, r22, parte, problemi);
        if (parte.Piste is { } np && perSezione.TryGetValue(np, out var r212)) scalo = scalo with { Runways = Piste(r212, icao, problemi) };
        // Segnalazioni 2611, B8 (LIET): le tabelle di AD 2.12 attaccate una sotto l'altra diventano una griglia sola e le piste
        // spariscono. Solo allora si rileggono le pagine spezzando le griglie dove cambiano le colonne: le altre pagine non
        // cambiano strada.
        if (parte.Piste is { } np2 && perSezione.ContainsKey(np2) && scalo.Runways.Count == 0)
        {
            var righe12 = new List<Riga>();
            var sez = 0;
            for (var i = 0; i < pages.Count; i++)
            {
                var piane = Table.PlainLinesWithY(pages[i]);
                var titoli = piane.Select(l => (l.Y, M: RxTitolo().Match(l.Text.Trim())))
                    .Where(x => x.M.Success && x.M.Groups["icao"].Value == icao)
                    .Select(x => (x.Y, N: int.Parse(x.M.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture))).ToList();
                foreach (var g in Table.FindAllSplit(pages[i], 300).OrderBy(g => g.Top))
                    if ((titoli.Where(t => t.Y < g.Top).Select(t => (int?)t.N).LastOrDefault() ?? sez) == np2)
                        righe12.AddRange(Righe(pages[i], g, pagine[i]));
                if (titoli.Count > 0) sez = titoli[^1].N;
            }
            if (Piste(righe12, icao, problemi) is { Count: > 0 } ritrovate) scalo = scalo with { Runways = ritrovate };
        }
        if (parte.Distanze is { } nd && perSezione.TryGetValue(nd, out var r213)) scalo = scalo with { DeclaredDistances = Distanze(r213) };
        if (perSezione.TryGetValue(parte.Spazi, out var r217)) aree.AddRange(Spazi(r217, icao, $"AD {parte.Numero}.{parte.Spazi}", problemi));
        if (perSezione.TryGetValue(parte.Frequenze, out var r218)) scalo = scalo with { Communications = Frequenze(r218) };
        if (perSezione.TryGetValue(parte.Radio, out var r219))
        {
            var (radioassistenze, ils) = Radioassistenze(r219, $"AD {parte.Numero}.{parte.Radio}", problemi);
            scalo = scalo with { NavigationAids = radioassistenze, Ils = ils };
        }
        // Tutte le sottosezioni, com'è stampate (carta «L'AIP intero», passo 5).
        var sottosezioni = ItalianAdSections.Read(pages, icao, pagine);
        scalo = scalo with { Sections = sottosezioni, Charts = Carte(sottosezioni, $"AD {parte.Numero}.{(parte == Ad3 ? 23 : 24)}") };
        if (scalo.Arp is null) problemi.Add(new ImportIssue(ImportIssueKind.Aerodrome, icao, null, $"senza ARP (AD {parte.Numero}.2)"));
        if (parte.Piste is not null && scalo.Runways.Count == 0) problemi.Add(new ImportIssue(ImportIssueKind.Aerodrome, icao, null, "senza piste (AD 2.12)"));
        return new ImportResult(aree, pagine, problemi) { Aerodromes = [scalo] };
    }

    /// <summary>
    /// Una tabella che continua nella pagina dopo con meno colonne di prima (LIRF AD 2.18 p.26: la tabella finisce presto, le
    /// note ATIS sotto sono una cella unita, e le righe interne non coprono metà della banda) prende quelle della pagina prima,
    /// spostate di quanto è sfalsata la pagina, se i bordi esterni tornano.
    /// </summary>
    private static TableGrid Completa(TableGrid g, TableGrid? prima)
    {
        if (prima is null || g.ColumnCount >= prima.ColumnCount) return g;
        var dx = g.Columns[0] - prima.Columns[0];
        if (Math.Abs(g.Columns[^1] - (prima.Columns[^1] + dx)) > 2) return g;
        return g with { Columns = [.. prima.Columns.Select(x => x + dx)] };
    }

    /// <summary>Le righe di una tabella sotto la sua riga dei numeri (o tutte, se non ne ha: AD 2.2), coi numeri della tabella.</summary>
    private static List<Riga> Righe(PdfPageText page, TableGrid g, ImportPage pagina)
    {
        var testo = Table.Lines(page, g);
        var righe = new List<Riga>();
        var numeri = "";
        var bande = Table.RowBands(page, g);
        var celleDi = bande.Select(b => Enumerable.Range(0, g.ColumnCount)
            .Select(c => testo.Where(l => l.Column == c && l.Y >= b.Top && l.Y < b.Bottom).OrderBy(l => l.Y).Select(l => l.Text.Trim()).ToList()).ToArray()).ToList();
        var iNumeri = celleDi.FindIndex(c => c.All(x => x.Count == 1 && x[0].All(char.IsDigit)));
        if (iNumeri >= 0) numeri = string.Join(' ', celleDi[iNumeri].Select(x => x[0]));
        for (var i = iNumeri + 1; i < celleDi.Count; i++)
            if (celleDi[i].Any(c => c.Count > 0)) righe.Add(new Riga(celleDi[i], pagina, numeri));
        return righe;
    }

    private static Aerodrome Dati(Aerodrome scalo, List<Riga> righe, Parte parte, List<ImportIssue> problemi)
    {
        foreach (var r in righe.Where(r => r.Celle.Length >= 3))
        {
            var valore = Unisci(r.Celle[2]);
            var numero = Unisci(r.Celle[0]);
            if (numero == parte.Arp)
            {
                // Gli eliporti scrivono «41°51'59'' N - 016°09'54'' E»: la lettera attaccata ai secondi, senza il trattino.
                var arp = valore is null ? null : RxSpazioPrimaDellEmisfero().Replace(RxTrattinoFraLeCoordinate().Replace(valore, "$1 "), "$1");
                scalo = scalo with { Arp = GeometryText.ReadPoint(arp), Page = Pagina(r.Pagina) };
                if (scalo.Arp is null) problemi.Add(new ImportIssue(ImportIssueKind.Aerodrome, scalo.Icao, r.Pagina.Number, $"ARP: {valore}"));
            }
            else if (numero == parte.Elevazione && valore is not null)
            {
                var (elev, temp) = Due(valore);
                scalo = scalo with { Elevation = elev, ReferenceTemperature = temp };
            }
            else if (numero == parte.Declinazione && valore is not null)
            {
                var (decl, anno) = Due(valore);
                scalo = scalo with { MagneticVariation = decl, AnnualChange = anno };
            }
            else if (numero == parte.Traffico)
                scalo = scalo with { Traffic = valore };
        }
        return scalo;
    }

    /// <summary>
    /// Le carte di AD 2.24 (AD 3.23): una riga per carta, il titolo e la pagina («AD 2 LICA 2 ­ 1», col trattino
    /// morbido): la pagina si scrive come nell'indice delle pagine, «AD 2 LICA 2-1». Null se la sottosezione non c'è.
    /// </summary>
    private static List<AerodromeChart>? Carte(IReadOnlyList<AerodromeSection> sottosezioni, string numero)
    {
        if (sottosezioni.All(s => s.Number != numero)) return null;
        var carte = new List<AerodromeChart>();
        // Da qualunque sottosezione: in LIPN il titolo di AD 2.25 sta sopra la tabella delle carte (2611).
        foreach (var r in sottosezioni.SelectMany(s => s.Rows ?? []))
        {
            if (r.Count < 2 || RxPaginaDiCarta().Match(r[^1].Trim()) is not { Success: true } m) continue;
            var titolo = RxSpazi().Replace(string.Join(" ", r.Take(r.Count - 1)), " ").Trim();
            carte.Add(new AerodromeChart(titolo, $"AD {m.Groups["p"].Value} {m.Groups["icao"].Value} {m.Groups["f"].Value}-{m.Groups["n"].Value}"));
        }
        return carte;
    }

    [GeneratedRegex(@"^AD\s+(?<p>[23])\s+(?<icao>[A-Z]{4})\s+(?<f>\d+)\s*[\-­–]\s*(?<n>\d+)$", Opzioni)]
    private static partial Regex RxPaginaDiCarta();

    /// <summary>«46 FT / 31.4° C» → («46 FT», «31.4° C»).</summary>
    private static (string?, string?) Due(string valore)
    {
        var i = valore.IndexOf(" / ", StringComparison.Ordinal);
        return i < 0 ? (valore.Trim(), null) : (valore[..i].Trim(), valore[(i + 3)..].Trim());
    }

    private static List<Runway> Piste(List<Riga> righe, string icao, List<ImportIssue> problemi)
    {
        // Le righe della prima tabella (colonne 1-6), le continuazioni unite alla riga sopra.
        var principali = Unite(righe.Where(r => r.Numeri.StartsWith("1 2 3", StringComparison.Ordinal)));
        var note = Unite(righe.Where(r => r.Numeri.EndsWith("12 13", StringComparison.Ordinal)))
            .ToDictionary(r => Unisci(r.Celle[0]) ?? "", r => r.Celle.Length > 2 ? SenzaNil(Unisci(r.Celle[2])) : null, StringComparer.Ordinal);
        var piste = new List<Runway>();
        foreach (var r in principali)
        {
            var c = r.Celle;
            if (c.Length < 6 || Unisci(c[0]) is not { } designatore) continue;
            var punti = GeometryText.ReadPoints(string.Join('\n', c[4]));
            // «NIL» stampato (LICP e altre 30 piste di campi piccoli nel 2611): la soglia non c'è, non è persa.
            if (punti.Count == 0 && Unisci(c[4]) is not (null or "NIL")) problemi.Add(new ImportIssue(ImportIssueKind.Aerodrome, icao, r.Pagina.Number, $"pista {designatore}: senza soglia"));
            piste.Add(new Runway
            {
                Designator = designatore,
                TrueBearing = Unisci(c[1]),
                Dimensions = Unisci(c[2]),
                Surface = Unisci(c[3]),
                Threshold = punti.Count > 0 ? punti[0] : null,
                End = punti.Count > 1 ? punti[1] : null,
                GeoidUndulation = c[4].Select(l => RxPiedi().Match(l)).FirstOrDefault(m => m.Success)?.Value,
                ThresholdElevation = Unisci(c[5]),
                Remarks = note.GetValueOrDefault(designatore),
            });
        }
        return piste;
    }

    /// <summary>Le righe senza la prima colonna (una tabella che continua nella pagina dopo) unite a quella sopra.</summary>
    private static List<Riga> Unite(IEnumerable<Riga> righe)
    {
        var unite = new List<Riga>();
        foreach (var r in righe)
        {
            if (r.Celle[0].Count == 0 && unite.Count > 0)
            {
                var sopra = unite[^1];
                for (var i = 0; i < Math.Min(sopra.Celle.Length, r.Celle.Length); i++) sopra.Celle[i].AddRange(r.Celle[i]);
                continue;
            }
            unite.Add(r with { Celle = [.. r.Celle.Select(c => c.ToList())] });
        }
        return unite;
    }

    private static List<DeclaredDistance> Distanze(List<Riga> righe)
    {
        var distanze = new List<DeclaredDistance>();
        foreach (var r in righe.Where(r => r.Celle.Length >= 5))
        {
            var c = r.Celle;
            if (c[0].Count == 0 || c[0][0].StartsWith("NOTE", StringComparison.OrdinalIgnoreCase)) continue;
            var pista = c[0][0];
            for (var i = 0; i < c[0].Count; i++)
            {
                string? Valore(int col) => i < c[col].Count ? c[col][i] : null;
                distanze.Add(new DeclaredDistance(pista, i == 0 ? null : c[0][i], Valore(1), Valore(2), Valore(3), Valore(4)));
            }
        }
        return distanze;
    }

    private static List<Airspace> Spazi(List<Riga> righe, string icao, string sezione, List<ImportIssue> problemi)
    {
        var spazi = new List<Airspace>();
        foreach (var r in Unite(righe).Where(r => r.Celle.Length >= 6))
        {
            var c = r.Celle;
            if (c[0].Count == 0) continue;
            var designatore = c[0][0];
            // Uno scalo senza spazio aereo suo: la tabella dice solo «NIL» (passo 5).
            if (designatore == "NIL") continue;
            // Il nome può andare a capo con il tipo dello spazio: «TORINO/Aeritalia» / «ATZ Sector 3», «Albenga/Riviera» /
            // «Airport ATZ» (segnalazioni 2611 B7: prima finivano nella geometria, e le tre ATZ di Aeritalia avevano lo stesso nome).
            var dopo = 1;
            while (dopo < c[0].Count && RxSeguitoDelNome().IsMatch(c[0][dopo].Trim()))
                designatore = $"{designatore} {c[0][dopo++].Trim()}";
            var letta = GeometryText.Read(string.Join('\n', c[0].Skip(dopo)));
            foreach (var g in letta.Issues)
                problemi.Add(new ImportIssue(ImportIssueKind.Geometry, designatore, r.Pagina.Number, g.Detail is null ? $"{g.Kind}" : $"{g.Kind}: {g.Detail}"));
            if (letta.Geometries.Count == 0)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, designatore, r.Pagina.Number));
                continue;
            }
            var limiti = ItalianAip.UnisciLimiti(c[1]);
            spazi.Add(new Airspace
            {
                Section = sezione,
                Type = designatore.Contains("ATZ", StringComparison.Ordinal) ? AirspaceType.Atz
                    : designatore.Contains("CTR", StringComparison.Ordinal) ? AirspaceType.Ctr : AirspaceType.Other,
                Designator = designatore,
                Upper = LimitText.Parse(limiti.Count > 0 ? limiti[0] : ""),
                // Se l'AIP stampa un limite solo (l'ATZ fino a 2000 FT AMSL), l'inferiore non c'è: NIL, non un suolo inventato.
                Lower = limiti.Count > 1 ? LimitText.Parse(limiti[1]) : new VerticalLimit { Text = "NIL" },
                Class = Unisci(c[2]),
                Unit = Unisci(c[3]),
                TransitionAltitude = Unisci(c[4]),
                Remarks = SenzaNil(Unisci(c[5])),
                Geometry = letta.Geometries[0],
                Page = Pagina(r.Pagina),
            });
            if (limiti.Count < 1) problemi.Add(new ImportIssue(ImportIssueKind.LimitsMissing, designatore, r.Pagina.Number, icao));
        }
        return spazi;
    }

    private static List<CommunicationFacility> Frequenze(List<Riga> righe)
    {
        var servizi = new List<CommunicationFacility>();
        foreach (var r in Unite(righe).Where(r => r.Celle.Length >= 5))
        {
            var c = r.Celle;
            if (c[0].Count == 0 || c[0][0].StartsWith("NOTE", StringComparison.OrdinalIgnoreCase)) continue;
            var servizio = c[0][0];
            var nominativo = SenzaNil(Unisci(c[1]));
            var orario = Unisci(c[3]);
            var note = SenzaNil(Unisci(c[4])) is { } n ? RxNumeroDellaNota().Replace(n, "\n") : null;
            // Più frequenze nella stessa cella: un servizio per frequenza.
            var frequenze = c[2].Select(f => f.Trim()).Where(f => RxFrequenza().IsMatch(f)).ToList();
            if (frequenze.Count == 0 && Unisci(c[2]) is { } altra) frequenze.Add(altra);
            foreach (var f in frequenze)
                servizi.Add(new CommunicationFacility(servizio, nominativo, f, orario, note));
        }
        return servizi;
    }

    /// <summary>AD 2.19: le radioassistenze dello scalo e gli ILS coi loro apparati.</summary>
    private static (List<Navaid>, List<Ils>) Radioassistenze(List<Riga> righe, string sezione, List<ImportIssue> problemi)
    {
        var radioassistenze = new List<Navaid>();
        var tuttiGliIls = new List<Ils>();
        Ils? ils = null;
        var apparati = new List<IlsComponent>();

        void ChiudiLIls()
        {
            if (ils is not null) tuttiGliIls.Add(ils with { Components = [.. apparati] });
            ils = null;
            apparati.Clear();
        }

        foreach (var r in Unite(righe).Where(r => r.Celle.Length >= 8))
        {
            var c = r.Celle;
            var primo = Unisci(c[0]);
            // Una tabella che dice solo «NIL» (uno scalo senza radioassistenze, carta «L'AIP intero», passo 5).
            if (primo is null or "NIL") continue;
            string? declinazione = null;
            if (RxDeclinazioneInFondo().Match(primo) is { Success: true } d)
            {
                declinazione = d.Groups["v"].Value.Trim();
                primo = primo[..d.Index].Trim();
            }
            var sigla = Unisci(c[1]) is { } x && x != "-" ? x : null;
            var (frequenza, canale) = ItalianEnr4.FrequenzaECanale(Unisci(c[2]));
            var orari = c[3].Select(t => t.Trim()).Where(t => t.Length > 0).ToList();
            var posti = new List<NavaidComponent>();
            foreach (var (apparato, testo) in ItalianEnr4.PerApparato(c[4]))
            {
                if (GeometryText.ReadPoint(testo) is not { } punto)
                {
                    problemi.Add(new ImportIssue(ImportIssueKind.Navaid, $"{sigla} {primo}", r.Pagina.Number, $"coordinate {apparato}: {testo}"));
                    continue;
                }
                posti.Add(new NavaidComponent(apparato, punto, Orario(orari, apparato)));
            }
            if (posti.Count == 0)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.Navaid, $"{sigla} {primo}", r.Pagina.Number, "senza coordinate"));
                continue;
            }
            var elevazione = SenzaNil(Unisci(c[5]));
            var copertura = SenzaNil(Unisci(c[6]));
            var note = SenzaNil(Unisci(c[7])) is { } n ? RxNumeroDellaNota().Replace(n, "\n") : null;

            if (RxIls().Match(primo) is { Success: true } m)
            {
                ChiudiLIls();
                if (sigla is null) problemi.Add(new ImportIssue(ImportIssueKind.Navaid, primo, r.Pagina.Number, "ILS senza sigla"));
                ils = new Ils
                {
                    Runway = m.Groups["pista"].Value,
                    Category = m.Groups["cat"].Success ? RxSpazi().Replace(m.Groups["cat"].Value, " ") : null,
                    Designator = sigla ?? "",
                    MagneticVariation = declinazione,
                    Components = [],
                    Page = Pagina(r.Pagina),
                };
                apparati.Add(new IlsComponent("LOC", posti[0].Position, frequenza, canale, posti[0].Hours, elevazione, copertura, note));
                continue;
            }
            if (ils is not null && ApparatiDellIls.Contains(primo) && (sigla is null || sigla == ils.Designator))
            {
                apparati.Add(new IlsComponent(primo, posti[0].Position, frequenza, canale, posti[0].Hours, elevazione, copertura, note));
                if (primo == "GP" && note is not null)
                    ils = ils with
                    {
                        Slope = RxPendenza().Match(note) is { Success: true } p ? RxSpazi().Replace(p.Groups["v"].Value, "") : ils.Slope,
                        ReferenceDatumHeight = RxRdh().Match(note) is { Success: true } h ? RxSpazi().Replace(h.Groups["v"].Value, " ") : ils.ReferenceDatumHeight,
                    };
                continue;
            }

            ChiudiLIls();
            var tipo = ItalianEnr4.Tipo(primo);
            if (sigla is null || tipo == NavaidKind.Other)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.Navaid, $"{sigla} {primo}", r.Pagina.Number, $"tipo o sigla: {primo} | {sigla}"));
                continue;
            }
            radioassistenze.Add(new Navaid
            {
                Section = sezione,
                Designator = sigla,
                Name = sigla,
                Type = primo,
                Kind = tipo,
                MagneticVariation = declinazione,
                Frequency = frequenza,
                Channel = canale,
                Position = posti[0].Position,
                Components = posti,
                Elevation = elevazione,
                Coverage = copertura,
                Remarks = note,
                Page = Pagina(r.Pagina),
            });
        }
        ChiudiLIls();
        return (radioassistenze, tuttiGliIls);
    }

    /// <summary>L'orario di un apparato («VOR H24» per il VOR); con un orario solo, quello.</summary>
    private static string? Orario(List<string> orari, string? apparato)
    {
        foreach (var o in orari)
            if (RxOrarioDiUnApparato().Match(o) is { Success: true } m && m.Groups["a"].Value == apparato)
                return m.Groups["h"].Value;
        return orari.Count == 1 ? orari[0] : null;
    }

    // --- la carta ATC SMAC ------------------------------------------------------------------------------------------------

    /// <summary>Il PDF è una carta ATC SMAC: una pagina ha il suo titolo.</summary>
    public static bool ESmac(IReadOnlyList<PdfPageText> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return pages.Any(p => Table.PlainLines(p).Any(l => l.Contains("SURVEILLANCE MINIMUM ALTITUDE", StringComparison.Ordinal)));
    }

    /// <summary>
    /// La carta ATC SMAC di uno scalo (carta F6, «finiamo con l'importer»): la tabella a due colonne delle pagine di testo —
    /// la quota minima, col nome del settore se c'è («5000FT NORTH-EAST») · i limiti laterali, come le geometrie di ENR 2
    /// («Line joining following points», «then arc of circle … centred on … till point»). Una riga senza quota continua il
    /// settore sopra (nella pagina dopo), e così la prima riga di una pagina con la stessa quota dell'ultimo settore (LICA,
    /// FL110 NORTH-WEST da p.5 a p.6). La nota in fondo alla carta («REMARK …», nella stessa banda dell'ultimo settore) non
    /// è geometria: si taglia. Le pagine della carta (immagini) non hanno la tabella: si contano e basta.
    /// </summary>
    public static ImportResult ReadSmac(IReadOnlyList<PdfPageText> pages, string icao)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var (righe, pagine, problemi) = ItalianEnr4.Righe(pages, 2);
        var settori = new List<(string Quota, List<string> Limiti, ImportPage Pagina)>();
        int? paginaDiPrima = null;
        foreach (var r in righe)
        {
            var quota = RxSpazi().Replace(string.Join(' ', r.Celle[0]), " ").Trim();
            var limiti = r.Celle[1].TakeWhile(l => !RxNotaDellaCarta().IsMatch(l.Trim())).ToList();
            var primaDellaPagina = r.Pagina.Number != paginaDiPrima;
            paginaDiPrima = r.Pagina.Number;
            if (settori.Count > 0 && (quota.Length == 0 || primaDellaPagina && quota == settori[^1].Quota)) settori[^1].Limiti.AddRange(limiti);
            else if (quota.Length > 0) settori.Add((quota, limiti, r.Pagina));
        }
        var aree = new List<MinimumAltitudeArea>();
        foreach (var (quota, limiti, pagina) in settori)
        {
            var chi = $"{icao} {quota}";
            var m = RxQuotaMinima().Match(quota);
            if (!m.Success)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.Aerodrome, chi, pagina.Number, $"SMAC, quota: {quota}"));
                continue;
            }
            var letta = GeometryText.Read(string.Join('\n', limiti));
            foreach (var g in letta.Issues)
                problemi.Add(new ImportIssue(ImportIssueKind.Geometry, chi, pagina.Number, g.Detail is null ? $"{g.Kind}" : $"{g.Kind}: {g.Detail}"));
            if (letta.Geometries.Count != 1)
            {
                problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, chi, pagina.Number, $"{letta.Geometries.Count} forme"));
                continue;
            }
            aree.Add(new MinimumAltitudeArea
            {
                Aerodrome = icao,
                Minimum = LimitText.Parse(m.Groups["q"].Value) with { Text = m.Groups["q"].Value },
                Name = m.Groups["n"].Value.Trim() is { Length: > 0 } n ? n : null,
                NoteRefs = m.Groups["r"].Success ? RxSpazi().Replace(m.Groups["r"].Value, "") : null,
                Geometry = letta.Geometries[0],
                Page = Pagina(pagina),
            });
        }
        return new ImportResult([], pagine, problemi) { MinimumAltitudes = aree };
    }

    private static AipPage? Pagina(ImportPage p) => p.Reference is null ? null : new AipPage(p.Reference, p.Effective);

    private static string? Unisci(List<string> righe)
    {
        var t = RxSpazi().Replace(string.Join(' ', righe), " ").Trim();
        return t.Length == 0 ? null : t;
    }

    private static string? SenzaNil(string? t) => t is null or "NIL" ? null : t;
}
