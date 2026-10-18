using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// ENR 2 dell'AIP di ENAV (carta F6, slice 1a): gli spazi aerei. Quattro famiglie di tabelle, un lettore solo:
/// <list type="bullet">
/// <item>tre colonne — nome e limiti laterali · limiti verticali e classe · ente (FIR/UIR, le TMA a zone, il FRAIT);</item>
/// <item>quattro colonne — … · nominativo e lingua · note (le CTA a zone, i CTR);</item>
/// <item>quattro colonne — … · altitudine di transizione · note (le ATZ militari, ENR 2.1.3);</item>
/// <item>quattro colonne — … · orari · note (RMZ, TMZ: ENR 2.2).</item>
/// </list>
/// In una pagina ci sono anche altre tabelle (frequenze, servizi, note): una tabella è delle aree se la sua testata dice
/// «limiti laterali», o se continua quella della pagina prima con le stesse colonne.
/// <para>Le aree le trova la prima colonna, con un automa — fuori, nome, geometria — e non un elenco di identificativi:
/// in ENR 2 i nomi sono liberi (<c>MILANO FIR</c>, <c>Alghero CTR</c>, <c>ATZ Crotone LIBC - RMZ</c>). Un titolo seguito
/// da una zona è un gruppo (<c>MILANO TMA</c> → <c>Zone '1' -LOMBARDIA-</c>). 🔴 La guardia resta: quel che nella
/// geometria non è né coordinata né frase si dice.</para>
/// </summary>
public static partial class ItalianEnr2
{
    private enum Ruolo { Geometria, Limiti, Ente, Orari, Transizione, Note, ClasseEdEnte }

    private sealed record Riga(int Pagina, int Indice, double Y, Ruolo Ruolo, string Testo, AirspaceType? TipoDellaSezione,
        (string? Sopra, string? Sotto) LimitiDelTesto);

    private sealed class AreaInLettura
    {
        public required string Designatore { get; init; }
        public string? Nome { get; set; }
        public string? Gruppo { get; init; }
        public required int Pagina { get; init; }
        public required int Indice { get; init; }
        public required double Y { get; init; }
        public AirspaceType? TipoDellaSezione { get; init; }
        public (string? Sopra, string? Sotto) LimitiDelTesto { get; init; }
        public List<string> Geometria { get; } = [];
        public string? Rimando { get; set; }
        public Dictionary<Ruolo, List<string>> Colonne { get; } = [];
        public bool Pozzo { get; init; }
    }

    // «Zone '1'», «Zona '29'» (il refuso della F0), e bilingue nei CTR: «Zona/Zone '2'».
    // Anche fra virgolette doppie: «Zona/Zone "2"» del CTR di Bergamo (ENR 2.1.2.6), che restava senza il suo CTR.
    [GeneratedRegex(@"^Zon[ae](?:\s*/\s*Zon[ae])?\s*['’""“”]?\s*\d+[A-Z]?\s*['’""“”]?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxZona();

    [GeneratedRegex(@"circular area|arc of circle|centred|centered|till point|point of origin|border|coast|linea congiungente|line joining|within a|radius|area circolare|arco di cerchio", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxFraseGeometrica();

    [GeneratedRegex(@"^(?:As for|Come per(?: la| il| lo)?)\s+(?<rif>.+?)\.?$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase)]
    private static partial Regex RxRimando();

    [GeneratedRegex(@"(?:point of origin|punto di origine)\W*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxChiusura();

    // Un cerchio si chiude col suo raggio («… within a 5.0 NM radius.»); «radius» in fondo a una riga d'arco no: lì va a capo.
    [GeneratedRegex(@"(?:circular area|area circolare).*(?:radius|raggio)\W*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RxChiusuraDelCerchio();

    [GeneratedRegex(@"\b(FIR|UIR|CTA|UTA|TMA|CTR|ATZ|RMZ|TMZ|NPZ|FRA)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxTipoNelNome();

    // Il titolo di una sezione di ENR 2.2 che dice il tipo: «ENR 2.2.1 ZONA RADIO OBBLIGATORIA (RMZ)», «ENR 2.2.4 No Planning Zone - NPZ».
    [GeneratedRegex(@"^ENR\s+2\.2\.\d+\b.*?\b(RMZ|TMZ|NPZ)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxTipoNelTitolo();

    [GeneratedRegex(@"LATERAL LIMITS|LIMITI LATERALI", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxTestataDelleAree();

    [GeneratedRegex(@"LIMITI LATERALI|LATERAL LIMITS|LIMITI VERTICALI|VERTICAL LIMITS|CLASSIFICA|UNIT PROVIDING|ENTE CHE FORNISCE|IL SERVIZIO|^SERVICE$|CALL SIGN|NOMINATIVO|LINGUA|LANGUAGE|ATS UNIT|UNITÀ ATS|TRANSITION|TRANSIZIONE|TIME OF ACTIVITY|ORARI|^NOTE$|^REMARKS$|^Remarks$|^Note$|DESIGNAT|IDENTIFICA|^VERTICAL LIMITS$|^Vertical limits$|AND CLASSIFICATION|DELL'UNITÀ|^UNIT$|^PROVIDING$|E UNITA|LIMITE SUPERIORE|LIMITE INFERIORE|UPPER LIMIT|LOWER LIMIT", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxTestata();

    // I limiti di una sezione detti nel testo (le FMC area: «•lower vertical limit: SFC», «•upper vertical limit: …»).
    [GeneratedRegex(@"(?<q>lower|upper) vertical limit:\s*(?<t>.+)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxLimiteNelTesto();

    [GeneratedRegex(@"\)\.\s+(?<seguito>[a-z].*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxSeguitoInglese();

    [GeneratedRegex(@"^ENR\s+\d+(\.\d+)+\.?\s", RegexOptions.CultureInvariant)]
    private static partial Regex RxTitoloDiSezione();

    [GeneratedRegex(@"^(\d\s+){2,}\d$")]
    private static partial Regex RxNumeriDelleColonne();

    [GeneratedRegex(@"^(UNL|GND|SFC|NIL|FL\s*\d{2,3}|\d[\d.,]*\s*(?:ft|FT|m|M)\b)", RegexOptions.CultureInvariant)]
    private static partial Regex RxInizioLimite();

    [GeneratedRegex(@"^(AMSL|AGL|MSL|ASFC|AAL|or\b|OR\b|o\b|whichever|is higher|is lower|oppure|il più)", RegexOptions.CultureInvariant)]
    private static partial Regex RxSeguitoDelLimite();

    [GeneratedRegex(@"^[""“”']?(?<c>[A-G])[""“”']?(?<rif>(\s*\d{1,2}\))*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxClasse();

    [GeneratedRegex(@"\s*(?<!\S)(\d{1,2})\)(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex RxRimandoAllaNota();

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    private const double TolleranzaY = 1.5;

    /// <summary>
    /// Le pagine di un PDF di ENR 2 nel modello. <paramref name="tipo"/> è il tipo di base del file (i CTR, le ATZ, il
    /// FRAIT), null dove lo dice il nome dell'area o il titolo della sezione.
    /// </summary>
    public static ImportResult Read(IReadOnlyList<PdfPageText> pages, string section, AirspaceType? tipo)
    {
        var problemi = new List<ImportIssue>();
        var pagine = new List<ImportPage>();
        var righe = new List<Riga>();
        var tipoDellaSezione = tipo;
        IReadOnlyList<Ruolo>? ruoliPrima = null;
        IReadOnlyList<double>? colonnePrima = null;
        (string? Sopra, string? Sotto) limitiDelTesto = (null, null);

        for (var k = 0; k < pages.Count; k++)
        {
            var page = pages[k];
            var testo = Table.PlainLinesWithY(page);
            limitiDelTesto = LimitiDelTesto(testo.Select(t => t.Text).ToList(), limitiDelTesto);
            var data = PageInfo.Date(testo.Select(t => t.Text));
            if (data is null) problemi.Add(new ImportIssue(ImportIssueKind.PageWithoutDate, null, page.Number));
            var griglie = Table.FindAll(page, minSpan: 300);
            var lette = 0;
            var sopraGià = double.MinValue;

            foreach (var griglia in griglie)
            {
                // Il tipo dal titolo sopra la tabella («ENR 2.2.1 ZONA RADIO OBBLIGATORIA (RMZ)»), se c'è.
                foreach (var (y, t) in testo.Where(t => t.Y < griglia.Top && t.Y > sopraGià))
                    if (RxTipoNelTitolo().Match(t) is { Success: true } m)
                        tipoDellaSezione = Tipo(m.Groups[1].Value);
                sopraGià = griglia.Bottom;

                var linee = Table.Lines(page, griglia);
                var primaCoordinata = linee.Where(l => l.Column == 0 && GeometryText.HasCoordinate(l.Text)).Select(l => l.Y).DefaultIfEmpty(double.MaxValue).Min();
                // La testata: le righe coi titoli delle colonne e quella dei numeri «1 2 3 4», che arriva colonna per colonna.
                // Anche il titolo della sottosezione, quando cade dentro la banda (ENR 2.1.1.2 p.17: «ENR 2.1.1.2.4.2 FMC
                // AREA: ROMA NORD» sopra la testata): preso per il primo dato, faceva della testata intera un nome.
                bool DiTestata(TextLine l) => RxTestata().IsMatch(l.Text.Trim()) || RxNumeriDelleColonne().IsMatch(l.Text.Trim())
                                              || RxTitoloDiSezione().IsMatch(l.Text.Trim())
                                              || l.Text.Trim() == (l.Column + 1).ToString(CultureInfo.InvariantCulture);
                // ⚠️ Solo SOPRA la prima riga di dati della prima colonna: una nota accanto al nome («I limiti laterali e
                // verticali si intendono…», le FMC area) dice le parole della testata, ma è un dato.
                var primoDato = linee.Where(l => l.Column == 0 && !DiTestata(l)).Select(l => l.Y).DefaultIfEmpty(double.MaxValue).Min();
                var testata = linee.Where(l => l.Y < Math.Min(primaCoordinata, primoDato) - TolleranzaY && DiTestata(l)).ToList();
                var testoDellaTestata = string.Join(' ', testata.Select(l => l.Text));

                IReadOnlyList<Ruolo>? ruoli;
                if (RxTestataDelleAree().IsMatch(testoDellaTestata))
                    ruoli = Ruoli(griglia.ColumnCount, testoDellaTestata);
                else if (ruoliPrima is not null && colonnePrima is not null && testata.Count == 0
                         && griglia.ColumnCount == colonnePrima.Count - 1
                         && griglia.Columns.Zip(colonnePrima).All(c => Math.Abs(c.First - c.Second) < 25))
                    ruoli = ruoliPrima;
                else
                    continue;
                if (ruoli is null) continue;

                ruoliPrima = ruoli;
                colonnePrima = griglia.Columns;
                lette++;
                var fineTestata = testata.Count > 0 ? testata.Max(l => l.Y) : double.MinValue;
                foreach (var l in linee)
                {
                    if (l.Y <= fineTestata + 1 || l.Column >= ruoli.Count) continue;
                    righe.Add(new Riga(page.Number, k, l.Y, ruoli[l.Column], l.Text.Trim(), tipoDellaSezione, limitiDelTesto));
                }
            }

            // I titoli sotto l'ultima tabella (o in una pagina senza tabelle) valgono per le pagine dopo: «ENR 2.2.4 No
            // Planning Zone - NPZ» sta a p.7, la sua tabella a p.8.
            foreach (var (y, t) in testo.Where(t => t.Y > sopraGià))
                if (RxTipoNelTitolo().Match(t) is { Success: true } m)
                    tipoDellaSezione = Tipo(m.Groups[1].Value);

            pagine.Add(new ImportPage(page.Number, PageInfo.Reference(testo.Select(t => t.Text)), data, lette > 0));
        }

        righe.Sort((a, b) => (a.Indice, a.Y, (int)a.Ruolo).CompareTo((b.Indice, b.Y, (int)b.Ruolo)));
        var aree = Componi(righe, problemi);

        var risultato = new List<Airspace>();
        foreach (var a in aree.Where(a => !a.Pozzo))
        {
            var area = Area(a, section, pagine, problemi, aree);
            if (area is not null) risultato.Add(area);
        }
        return new ImportResult(risultato, pagine, problemi);
    }

    private static IReadOnlyList<Ruolo>? Ruoli(int colonne, string testata) => colonne switch
    {
        // Le FMC area: «classificazione e unità ATS», niente limiti verticali in tabella (li dice il testo della sezione).
        3 when !Regex.IsMatch(testata, "VERTICAL|UPPER LIMIT|LIMITE SUPERIORE", RegexOptions.IgnoreCase) => [Ruolo.Geometria, Ruolo.ClasseEdEnte, Ruolo.Note],
        // Le NPZ: «limite superiore - limite inferiore · note», senza ente.
        3 when !Regex.IsMatch(testata, "UNIT|ENTE|SERVIZIO|SERVICE", RegexOptions.IgnoreCase) => [Ruolo.Geometria, Ruolo.Limiti, Ruolo.Note],
        3 => [Ruolo.Geometria, Ruolo.Limiti, Ruolo.Ente],
        4 when Regex.IsMatch(testata, "TRANSIZIONE|TRANSITION", RegexOptions.IgnoreCase) => [Ruolo.Geometria, Ruolo.Limiti, Ruolo.Transizione, Ruolo.Note],
        4 when Regex.IsMatch(testata, "ORARI|TIME OF", RegexOptions.IgnoreCase) => [Ruolo.Geometria, Ruolo.Limiti, Ruolo.Orari, Ruolo.Note],
        4 => [Ruolo.Geometria, Ruolo.Limiti, Ruolo.Ente, Ruolo.Note],
        _ => null,
    };

    /// <summary>L'automa della prima colonna: dove comincia un'area, il suo nome, la sua geometria.</summary>
    private static List<AreaInLettura> Componi(List<Riga> righe, List<ImportIssue> problemi)
    {
        var prime = righe.Where(r => r.Ruolo == Ruolo.Geometria).ToList();
        var aree = new List<AreaInLettura>();
        AreaInLettura? aperta = null;
        var stato = 0; // 0 fuori, 1 nome, 2 geometria
        string? gruppo = null;

        for (var i = 0; i < prime.Count; i++)
        {
            var r = prime[i];
            var t = r.Testo;
            var geo = GeometryText.HasCoordinate(t) || RxFraseGeometrica().IsMatch(t);
            var rimando = RxRimando().Match(t);

            if (stato == 1 && rimando.Success)
            {
                aperta!.Rimando = rimando.Groups["rif"].Value.Trim();
                continue;
            }
            if (stato == 1 && !geo && aperta!.Rimando is not null)
            {
                // Dopo un rimando la riga dopo è la prossima area (l'italiano e l'inglese del rimando sono già presi).
                stato = 0;
            }

            if (stato == 2 && !geo && (RxZona().IsMatch(t) || RxTipoNelNome().IsMatch(t)))
            {
                problemi.Add(new ImportIssue(ImportIssueKind.Geometry, Chiave(aperta!), r.Pagina, "NotClosed: comincia un'altra area"));
                stato = 0;
            }

            switch (stato)
            {
                case 0 when !geo:
                    var successiva = i + 1 < prime.Count ? prime[i + 1].Testo : null;
                    if (!RxZona().IsMatch(t) && successiva is not null && RxZona().IsMatch(successiva))
                    {
                        // Un titolo seguito da una zona è un gruppo: non è un'area, ma le sue note vanno da qualche parte.
                        gruppo = t;
                        aree.Add(new AreaInLettura { Designatore = t, Pagina = r.Pagina, Indice = r.Indice, Y = r.Y, Pozzo = true, TipoDellaSezione = r.TipoDellaSezione });
                        aperta = null;
                        continue;
                    }
                    aperta = Nuova(t, r, RxZona().IsMatch(t) ? gruppo : null);
                    aree.Add(aperta);
                    stato = 1;
                    continue;

                case 0:
                    // Geometria senza un nome davanti: va all'ultima area (la guardia della geometria dirà il resto).
                    if (aree.LastOrDefault(a => !a.Pozzo) is { } ultima)
                    {
                        ultima.Geometria.Add(t);
                        problemi.Add(new ImportIssue(ImportIssueKind.UnknownIdentifier, Chiave(ultima), r.Pagina, "geometria dopo la chiusura: " + t));
                    }
                    else
                    {
                        problemi.Add(new ImportIssue(ImportIssueKind.UnknownIdentifier, null, r.Pagina, t));
                    }
                    continue;

                case 1 when !geo:
                    aperta!.Nome = aperta.Nome is null ? t : aperta.Nome + " " + t;
                    continue;

                default:
                    aperta!.Geometria.Add(t);
                    var finora = string.Join(' ', aperta.Geometria);
                    stato = RxChiusura().IsMatch(finora) || RxChiusuraDelCerchio().IsMatch(finora) ? 0 : 2;
                    continue;
            }
        }

        // Ogni riga delle altre colonne va all'ultima area (o gruppo) cominciata sopra di lei.
        foreach (var r in righe.Where(r => r.Ruolo != Ruolo.Geometria))
        {
            AreaInLettura? proprietaria = null;
            foreach (var a in aree)
            {
                if (r.Indice > a.Indice || (r.Indice == a.Indice && r.Y >= a.Y - TolleranzaY)) proprietaria = a;
                else break;
            }
            if (proprietaria is null) continue;
            if (!proprietaria.Colonne.TryGetValue(r.Ruolo, out var l)) proprietaria.Colonne[r.Ruolo] = l = [];
            l.Add(r.Testo);
        }
        return aree;

        static AreaInLettura Nuova(string t, Riga r, string? gruppo)
        {
            string designatore = t;
            string? nome = null;
            if (RxZona().Match(t) is { Success: true } z)
            {
                designatore = (gruppo is null ? "" : gruppo + " ") + RxSpazi().Replace(z.Value.Trim(), " ");
                var resto = t[z.Length..].Trim().Trim('-', '–', ' ').Trim();
                nome = resto.Length > 0 ? resto : null;
            }
            return new AreaInLettura
            {
                Designatore = RxSpazi().Replace(designatore, " ").Trim(),
                Nome = nome,
                Gruppo = gruppo,
                Pagina = r.Pagina,
                Indice = r.Indice,
                Y = r.Y,
                TipoDellaSezione = r.TipoDellaSezione,
                LimitiDelTesto = r.LimitiDelTesto,
            };
        }
    }

    private static Airspace? Area(AreaInLettura a, string section, List<ImportPage> pagine, List<ImportIssue> problemi, List<AreaInLettura> tutte)
    {
        var chiave = Chiave(a);
        var pagina = pagine.First(p => p.Number == a.Pagina);
        var rimandi = new SortedSet<int>();

        Geometry? geometria = null;
        if (a.Rimando is not null)
        {
            var parole = Parole(a.Rimando);
            var origine = tutte.FirstOrDefault(x => !x.Pozzo && x != a && x.Rimando is null && Parole(x.Designatore).SetEquals(parole));
            if (origine is not null)
                geometria = GeometryText.Read(string.Join('\n', origine.Geometria)).Geometries.FirstOrDefault();
            if (geometria is null)
                problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, chiave, a.Pagina, "rimando non trovato: " + a.Rimando));
        }
        else
        {
            var letta = GeometryText.Read(string.Join('\n', a.Geometria));
            foreach (var g in letta.Issues)
                problemi.Add(new ImportIssue(ImportIssueKind.Geometry, chiave, a.Pagina, g.Detail is null ? $"{g.Kind}" : $"{g.Kind}: {g.Detail}"));
            if (letta.Geometries.Count > 1)
                problemi.Add(new ImportIssue(ImportIssueKind.MoreGeometries, chiave, a.Pagina, letta.Geometries.Count.ToString(CultureInfo.InvariantCulture)));
            geometria = letta.Geometries.FirstOrDefault();
            if (geometria is null)
                problemi.Add(new ImportIssue(ImportIssueKind.NoGeometry, chiave, a.Pagina));
        }
        if (geometria is null) return null;

        var classeEdEnte = Colonna(a, Ruolo.ClasseEdEnte);
        List<VerticalBand> bande;
        List<string> extra;
        string? ente;
        if (classeEdEnte.Count > 0 || (Colonna(a, Ruolo.Limiti).Count == 0 && a.LimitiDelTesto.Sopra is not null))
        {
            // Senza la colonna dei limiti: la classe e l'ente da «classificazione e unità ATS», i limiti dal testo della sezione.
            var classe = classeEdEnte.Select(r => RxClasse().Match(TogliRimandi(r, rimandi))).FirstOrDefault(m => m.Success)?.Groups["c"].Value;
            var enti = classeEdEnte.Select(r => TogliRimandi(r, rimandi)).Where(r => r.Length > 0 && !RxClasse().IsMatch(r)).ToList();
            ente = enti.Count > 0 ? string.Join("\n", enti) : null;
            extra = [];
            bande = [];
            if (a.LimitiDelTesto is { Sopra: { } sopra, Sotto: { } sotto })
            {
                bande.Add(new VerticalBand(LimitText.Parse(sopra), LimitText.Parse(sotto), classe));
                problemi.Add(new ImportIssue(ImportIssueKind.LimitsFromText, chiave, a.Pagina, $"{sopra} / {sotto}"));
            }
            else
            {
                problemi.Add(new ImportIssue(ImportIssueKind.LimitsMissing, chiave, a.Pagina));
            }
        }
        else
        {
            (bande, extra) = Fasce(Colonna(a, Ruolo.Limiti), rimandi, chiave, a.Pagina, problemi);
            ente = UnisciARighe(Colonna(a, Ruolo.Ente), rimandi);
        }
        var note = new List<string>();
        if (Unisci(Colonna(a, Ruolo.Note), rimandi) is { } n) note.Add(n);
        if (extra.Count > 0) note.Add(string.Join(' ', extra));
        if (rimandi.Count > 0) note.Add("Note " + string.Join(" ", rimandi.Select(x => x + ")")));

        // In ENR 2.2 comanda il titolo della sezione: «ATZ Crotone LIBC - RMZ» sta fra le RMZ ed è una RMZ.
        var tipo = (a.TipoDellaSezione is AirspaceType.Rmz or AirspaceType.Tmz or AirspaceType.Npz ? a.TipoDellaSezione : null)
                   ?? Tipo(a.Designatore) ?? (a.Gruppo is null ? null : Tipo(a.Gruppo)) ?? a.TipoDellaSezione ?? AirspaceType.Other;
        return new Airspace
        {
            Section = section,
            Type = tipo,
            Designator = a.Designatore,
            Name = a.Nome,
            Upper = bande.Count > 0 ? bande[0].Upper : new VerticalLimit { Text = "" },
            Lower = bande.Count > 0 ? bande[^1].Lower : new VerticalLimit { Text = "" },
            Class = bande.Count == 1 ? bande[0].Class : null,
            Bands = bande.Count > 1 ? bande : null,
            Unit = ente,
            Hours = Unisci(Colonna(a, Ruolo.Orari), rimandi),
            TransitionAltitude = Unisci(Colonna(a, Ruolo.Transizione), rimandi),
            Remarks = note.Count > 0 ? string.Join("\n", note) : null,
            Geometry = geometria,
            Page = pagina.Reference is null ? null : new AipPage(pagina.Reference, pagina.Effective),
        };
    }

    /// <summary>
    /// La colonna dei limiti in fasce: i limiti (un limite andato a capo si riunisce) e, dopo ogni coppia, la classe se
    /// c'è (<c>FL195 · SFC · "G"</c>; la UIR: <c>UNL · FL660 · "G" · FL660 · FL195 · "C"</c>). Quel che non è né limite né
    /// classe va nelle note. I rimandi alle note (<c>1) 2)</c>) si tolgono e si contano.
    /// </summary>
    internal static (List<VerticalBand> Bande, List<string> Extra) Fasce(IReadOnlyList<string> righe, SortedSet<int> rimandi, string? chiave, int pagina, List<ImportIssue> problemi)
    {
        var bande = new List<VerticalBand>();
        var limiti = new List<string>();
        var extra = new List<string>();
        foreach (var grezza in righe)
        {
            var r = TogliRimandi(grezza, rimandi);
            if (r.Length == 0) continue;
            if (RxClasse().Match(r) is { Success: true } c)
            {
                ChiudiFascia(c.Groups["c"].Value);
                continue;
            }
            if (RxInizioLimite().IsMatch(r)) limiti.Add(r);
            else if (limiti.Count > 0 && RxSeguitoDelLimite().IsMatch(r)) limiti[^1] += " " + r;
            else extra.Add(r);
        }
        if (limiti.Count > 0) ChiudiFascia(null);
        return (bande, extra);

        void ChiudiFascia(string? classe)
        {
            if (limiti.Count >= 2)
            {
                if (limiti.Count > 2)
                    problemi.Add(new ImportIssue(ImportIssueKind.LimitsExtra, chiave, pagina, string.Join(" | ", limiti.Take(limiti.Count - 2))));
                bande.Add(new VerticalBand(LimitText.Parse(limiti[^2]), LimitText.Parse(limiti[^1]), classe));
            }
            else if (limiti.Count == 1 && bande.Count > 0)
            {
                // Una fascia che comincia dove finisce quella prima («FL660» stampato una volta sola).
                bande.Add(new VerticalBand(bande[^1].Lower, LimitText.Parse(limiti[0]), classe));
            }
            else
            {
                problemi.Add(new ImportIssue(ImportIssueKind.LimitsMissing, chiave, pagina, string.Join(" | ", limiti)));
            }
            limiti.Clear();
        }
    }

    /// <summary>
    /// I limiti verticali detti nel testo di una sezione invece che nella tabella (ENR 2.1.1.x.5, le FMC area: «•limite
    /// verticale inferiore: SFC •lower vertical limit: SFC»). Si prende la metà inglese, col seguito della riga dopo
    /// («… of overlying controlled» / «… ecc.). airspace (CTR, TMA, …).»). Valgono finché un'altra sezione non ne dice altri.
    /// </summary>
    internal static (string? Sopra, string? Sotto) LimitiDelTesto(IReadOnlyList<string> righe, (string? Sopra, string? Sotto) prima)
    {
        string? sopra = null, sotto = null;
        for (var i = 0; i < righe.Count; i++)
        {
            foreach (var pezzo in righe[i].Split('•'))
            {
                if (RxLimiteNelTesto().Match(pezzo.Trim()) is not { Success: true } m) continue;
                var testo = m.Groups["t"].Value.Trim();
                if (!testo.EndsWith('.') && !testo.EndsWith(')') && i + 1 < righe.Count && RxSeguitoInglese().Match(righe[i + 1]) is { Success: true } s)
                    testo += " " + s.Groups["seguito"].Value.Trim();
                testo = testo.TrimEnd('.');
                if (m.Groups["q"].Value == "upper") sopra = testo;
                else sotto = testo;
            }
        }
        return sopra is not null && sotto is not null ? (sopra, sotto) : prima;
    }

    private static string TogliRimandi(string testo, SortedSet<int> rimandi)
    {
        var pulito = RxRimandoAllaNota().Replace(testo, m =>
        {
            rimandi.Add(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            return "";
        });
        return RxSpazi().Replace(pulito, " ").Trim();
    }

    private static IReadOnlyList<string> Colonna(AreaInLettura a, Ruolo r) => a.Colonne.TryGetValue(r, out var l) ? l : [];

    private static string? Unisci(IReadOnlyList<string> righe, SortedSet<int> rimandi)
    {
        var t = RxSpazi().Replace(string.Join(' ', righe), " ").Trim();
        return t.Length == 0 ? null : t;
    }

    private static string? UnisciARighe(IReadOnlyList<string> righe, SortedSet<int> rimandi)
    {
        var pulite = righe.Select(r => TogliRimandi(r, rimandi)).Where(r => r.Length > 0).ToList();
        return pulite.Count == 0 ? null : string.Join("\n", pulite);
    }

    private static string Chiave(AreaInLettura a) => a.Nome is null ? a.Designatore : $"{a.Designatore} {a.Nome}";

    private static HashSet<string> Parole(string testo) =>
        RxSpazi().Split(testo.ToUpperInvariant().Trim('.', ' '))
            .Where(p => p is not ("LA" or "IL" or "LO" or "THE"))
            .ToHashSet(StringComparer.Ordinal);

    private static AirspaceType? Tipo(string testo) => RxTipoNelNome().Match(testo) is { Success: true } m
        ? m.Groups[1].Value switch
        {
            "FIR" => AirspaceType.Fir,
            "UIR" => AirspaceType.Uir,
            "CTA" => AirspaceType.Cta,
            "UTA" => AirspaceType.Uta,
            "TMA" => AirspaceType.Tma,
            "CTR" => AirspaceType.Ctr,
            "ATZ" => AirspaceType.Atz,
            "RMZ" => AirspaceType.Rmz,
            "TMZ" => AirspaceType.Tmz,
            "NPZ" => AirspaceType.Npz,
            "FRA" => AirspaceType.Fra,
            _ => null,
        }
        : null;
}
