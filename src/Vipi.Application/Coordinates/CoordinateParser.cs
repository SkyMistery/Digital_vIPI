using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Application.Aor;

namespace Vipi.Application.Coordinates;

/// <summary>
/// Legge coordinate scritte <b>come capita</b> e le riduce ad aree di vertici. Puro, deterministico, senza I/O.
///
/// <para>Le forme riconosciute (carta <c>docs/feature/2026-08-29-convertitore-coordinate.md</c> §3): DMS Aurora
/// puntato e compatto, DMS coi simboli, DMS coi due punti, DMS a spazi, gradi e primi decimali, ARINC a
/// larghezza fissa, decimale con segno o con emisfero, coppia <c>lat:lon</c> del DB IVAO, CSV/Google Maps,
/// sectorfile a punti e a segmenti, JSON/GeoJSON.</para>
///
/// <para>⚠️ <b>Nulla si scarta in silenzio</b>: ogni riga non letta esce in <see cref="CoordinateIssue"/> col suo
/// numero di riga. Un convertitore che ne perde tre su venti senza dirlo è peggio di uno che rifiuta tutto.</para>
/// </summary>
public static class CoordinateParser
{
    /// <summary>
    /// Il tetto delle righe. Non è una difesa dal disco ma dal <b>circuito</b>: il testo attraversa Blazor
    /// Server, e <c>itgeo.geo</c> intero sono decine di migliaia di righe. Stessa ragione del
    /// <c>MaxRigheDiff</c> del Profile Swapper.
    /// </summary>
    public const int MaxRighe = 5000;

    /// <summary>Quanto due punti devono somigliarsi per dirsi lo stesso vertice: 1e-6° ≈ 11 cm.</summary>
    private const double StessoPunto = 1e-6;

    private const RegexOptions Opzioni = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    // ---- I riconoscitori, dal più specifico al più generico. L'ordine È la regola. ----

    /// <summary>DMS Aurora puntato: <c>N041.59.26.000</c>.</summary>
    private static readonly Regex RxAuroraPuntata =
        new(@"^[NSEW]\d{1,3}\.\d{1,2}\.\d{1,2}(?:\.\d{1,3})?$", Opzioni);

    /// <summary>DMS Aurora compatto: <c>N0463144000</c>.</summary>
    private static readonly Regex RxAuroraCompatta = new(@"^[NSEW]\d{8,11}$", Opzioni);

    /// <summary>Coppia ARINC/ICAO a larghezza fissa: <c>4159N01159E</c>, <c>411500N0115730E</c>.</summary>
    private static readonly Regex RxArinc = new(
        @"^(?<a>\d{2,9}(?:\.\d+)?)(?<ha>[NSEW])(?<b>\d{2,9}(?:\.\d+)?)(?<hb>[NSEW])$", Opzioni);

    /// <summary>Coppia ARINC con l'emisfero davanti: <c>N4159E01159</c>.</summary>
    private static readonly Regex RxArincEmisferoDavanti = new(
        @"^(?<ha>[NSEW])(?<a>\d{2,9}(?:\.\d+)?)(?<hb>[NSEW])(?<b>\d{2,9}(?:\.\d+)?)$", Opzioni);

    /// <summary>DMS/DM coi simboli: <c>41°59'26.5"N</c>, <c>N41°59.433'</c>, <c>41°N</c>.</summary>
    private static readonly Regex RxSimboli = new(
        @"^(?<h1>[NSEW])?(?<d>\d{1,3})°(?:(?<m>\d{1,2}(?:\.\d+)?)['′](?:(?<s>\d{1,2}(?:\.\d+)?)[""″]?)?)?(?<h2>[NSEW])?$",
        Opzioni);

    /// <summary>Numero semplice, con o senza emisfero: <c>41.9906</c>, <c>-11.98</c>, <c>N41.9906</c>, <c>4159.433N</c>.</summary>
    private static readonly Regex RxSemplice =
        new(@"^(?<h1>[NSEW])?(?<v>[+-]?\d+(?:\.\d+)?)(?<h2>[NSEW])?$", Opzioni);

    /// <summary>DMS a spazi (<c>41 59 26 N</c>) → forma simbolica, prima di spezzare la riga sugli spazi.</summary>
    private static readonly Regex RxSpaziEmisferoDietro =
        new(@"(?<![\d.])(?<d>\d{1,3})[ ]+(?<m>\d{1,2})[ ]+(?<s>\d{1,2}(?:\.\d+)?)[ ]*(?<h>[NSEW])(?![\dA-Z])", Opzioni);

    /// <summary>DMS a spazi con l'emisfero davanti: <c>N 41 59 26</c>.</summary>
    private static readonly Regex RxSpaziEmisferoDavanti =
        new(@"(?<![A-Z\d])(?<h>[NSEW])[ ]+(?<d>\d{1,3})[ ]+(?<m>\d{1,2})[ ]+(?<s>\d{1,2}(?:\.\d+)?)(?![\d.])", Opzioni);

    /// <summary>
    /// DMS coi due punti: <c>41:59:26.5</c>. ⚠️ Serve <b>due</b> volte il separatore, o si mangerebbe il
    /// <c>lat:lon</c> del DB IVAO, che di due punti ne ha uno solo.
    /// </summary>
    private static readonly Regex RxDuePunti =
        new(@"(?<![\d:])(?<d>\d{1,3}):(?<m>\d{1,2}):(?<s>\d{1,2}(?:\.\d+)?)(?![\d:])", Opzioni);

    private static readonly char[] Separatori = [';', ',', '|', '\t', ' ', ':'];

    /// <summary>Legge il testo. Non lancia mai: ciò che non si capisce esce come segnalazione.</summary>
    public static CoordinateReadResult Parse(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return CoordinateReadResult.Vuoto;

        // KML incollato come testo: è lo stesso lettore del file caricato, non un secondo dispatch. Da qui in
        // poi un KML è aree e punti come tutto il resto (carta §10, domanda 2).
        var trimmed = testo.TrimStart();
        if (trimmed.StartsWith('<')) return KmlReader.LeggiKml(testo);

        // JSON/GeoJSON: è un contenitore intero, non una riga. ⚠️ Lì la LONGITUDINE viene prima (regola IVAO
        // regionMapPolygon), e la verità su quell'ordine sta in PolygonGeometry: qui non si riscrive.
        if (trimmed.StartsWith('[') || trimmed.StartsWith('{'))
        {
            var daJson = PolygonGeometry.ParsePoints(testo);
            if (daJson.Count > 0) return DaPunti(daJson, testo);
        }

        var righe = testo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var segnalazioni = new List<CoordinateIssue>();
        var quante = righe.Length;
        if (quante > MaxRighe)
        {
            segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.TroppeRighe, 0, "",
                $"{quante} > {MaxRighe}"));
            quante = MaxRighe;
        }

        var gruppi = new List<Gruppo>();
        Gruppo? anonimo = null;
        var perNome = new Dictionary<string, Gruppo>(StringComparer.OrdinalIgnoreCase);
        var lette = 0;

        for (var i = 0; i < quante; i++)
        {
            var numero = i + 1;
            var originale = righe[i];
            if (string.IsNullOrWhiteSpace(originale))
            {
                // ⚠️ La riga vuota CHIUDE il blocco anonimo, come fa il parser dei .vfi: due elenchi di vertici
                // separati da una riga bianca sono due aree, non una con un salto.
                anonimo = null;
                continue;
            }

            // ⚠️ Una riga di solo commento NON chiude niente: gli header dei .geo (`//PENISOLA`) stanno in mezzo
            // ai vertici, e trattarli come separatori spezzerebbe in due un'area sola.
            var riga = TogliCommento(originale);
            if (riga.Length == 0) continue;

            var (angoli, etichette) = LeggiRiga(riga);
            var punti = new List<(double Lat, double Lon)>();
            var problema = false;

            for (var k = 0; k + 1 < angoli.Count; k += 2)
            {
                if (!ProvaCoppia(angoli[k], angoli[k + 1], out var punto, out var avviso))
                {
                    segnalazioni.Add(new CoordinateIssue(avviso ?? CoordinateIssueKind.RigaNonLetta, numero, originale.Trim()));
                    problema = true;
                    continue;
                }
                if (avviso is { } a) segnalazioni.Add(new CoordinateIssue(a, numero, originale.Trim()));
                punti.Add(punto);
            }

            if (angoli.Count % 2 != 0)
                segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.AngoloSpaiato, numero, originale.Trim()));

            if (punti.Count == 0)
            {
                if (!problema && angoli.Count % 2 == 0)
                    segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.RigaNonLetta, numero, originale.Trim()));
                continue;
            }

            lette++;

            // 5° campo = tipo, 6° = nome (italy.restrict). Un .geo ha il solo tipo, e nome non ne ha.
            string? tipo = etichette.Count > 0 ? etichette[0] : null;
            string? nome = etichette.Count > 1 ? etichette[^1] : null;

            // Due punti su una riga CON un'etichetta = un segmento (è la forma di italy.restrict, dove il tipo
            // c'è sempre). Due punti nudi sono due vertici scritti sulla stessa riga.
            var segmento = punti.Count == 2 && etichette.Count > 0;

            var gruppo = Prendi(nome, ref anonimo, perNome, gruppi);
            gruppo.Tipo ??= tipo;
            if (segmento)
            {
                gruppo.Segmenti.Add((punti[0], punti[1], numero));
                gruppo.DaSegmenti = true;
            }
            else
            {
                gruppo.Vertici.AddRange(punti);
            }
        }

        var aree = new List<CoordinateArea>();
        foreach (var g in gruppi)
        {
            var area = g.DaSegmenti ? DaSegmenti(g, segnalazioni) : DaVertici(g);
            if (area is not null) aree.Add(area);
        }

        return new CoordinateReadResult(aree, segnalazioni, lette, righe.Length);
    }

    // ---- I gruppi: un'area per nome, più i blocchi anonimi separati dalle righe vuote ----

    private sealed class Gruppo
    {
        public string? Nome { get; init; }
        public string? Tipo { get; set; }
        public bool DaSegmenti { get; set; }
        public List<(double Lat, double Lon)> Vertici { get; } = [];
        public List<((double Lat, double Lon) A, (double Lat, double Lon) B, int Riga)> Segmenti { get; } = [];
    }

    private static Gruppo Prendi(string? nome, ref Gruppo? anonimo,
        Dictionary<string, Gruppo> perNome, List<Gruppo> tutti)
    {
        if (nome is not null)
        {
            // ⚠️ Per NOME, non per posizione: in italy.restrict le righe di R107A e R107B si alternano.
            if (perNome.TryGetValue(nome, out var g)) return g;
            g = new Gruppo { Nome = nome };
            perNome[nome] = g;
            tutti.Add(g);
            return g;
        }

        if (anonimo is null)
        {
            anonimo = new Gruppo();
            tutti.Add(anonimo);
        }
        return anonimo;
    }

    private static CoordinateArea? DaVertici(Gruppo g)
    {
        var punti = new List<(double Lat, double Lon)>(g.Vertici);
        if (punti.Count == 0) return null;

        // L'ultimo vertice che torna sul primo non è un punto in più: è la chiusura dell'anello.
        var chiuso = punti.Count > 2 && Stesso(punti[0], punti[^1]);
        if (chiuso) punti.RemoveAt(punti.Count - 1);

        return new CoordinateArea(g.Nome, punti, chiuso, g.Tipo);
    }

    private static CoordinateArea? DaSegmenti(Gruppo g, List<CoordinateIssue> segnalazioni)
    {
        if (g.Segmenti.Count == 0) return null;

        var punti = new List<(double Lat, double Lon)>();
        (double Lat, double Lon)? fine = null;   // la FINE del segmento precedente: e' lei che deve combaciare
        for (var i = 0; i < g.Segmenti.Count; i++)
        {
            var (a, b, riga) = g.Segmenti[i];

            // ⚠️ Il vertice di un segmento è il suo INIZIO: ogni lato ne porta uno, e l'ultimo porta anche la
            // sua fine se l'anello non si chiude. Prendere l'inizio solo quando la catena si spezza (il primo
            // tentativo) restituiva UN punto su cinque, e il test lo ha detto subito.
            if (fine is not null && !Stesso(fine.Value, a))
            {
                // Si SEGNALA e si prosegue, non si aggiusta: segmenti scollegati non sono un poligono, ed è
                // più probabile che sia un incolla parziale che un'intenzione. Ma buttare la riga sarebbe
                // peggio: chi guarda la mappa vede il salto e capisce da solo dove ha tagliato.
                segnalazioni.Add(new CoordinateIssue(CoordinateIssueKind.CatenaInterrotta, riga,
                    Punto(a), Punto(fine.Value)));
            }

            punti.Add(a);
            if (i == g.Segmenti.Count - 1 && !Stesso(b, punti[0])) punti.Add(b);
            fine = b;
        }

        var chiuso = Stesso(g.Segmenti[^1].B, punti[0]);
        return new CoordinateArea(g.Nome, punti, chiuso, g.Tipo, DaSegmenti: true);
    }

    private static CoordinateReadResult DaPunti(IReadOnlyList<(double Lat, double Lon)> punti, string testo)
    {
        var lista = new List<(double Lat, double Lon)>(punti);
        var chiuso = lista.Count > 2 && Stesso(lista[0], lista[^1]);
        if (chiuso) lista.RemoveAt(lista.Count - 1);
        var righe = testo.Replace("\r\n", "\n").Split('\n').Length;
        return new CoordinateReadResult([new CoordinateArea(null, lista, chiuso)], [], righe, righe);
    }

    private static bool Stesso((double Lat, double Lon) a, (double Lat, double Lon) b) =>
        Math.Abs(a.Lat - b.Lat) < StessoPunto && Math.Abs(a.Lon - b.Lon) < StessoPunto;

    private static string Punto((double Lat, double Lon) p) =>
        string.Create(CultureInfo.InvariantCulture, $"{p.Lat:0.######}, {p.Lon:0.######}");

    // ---- La riga: normalizzazione, spezzettamento, riconoscimento ----

    private static string TogliCommento(string riga)
    {
        var i = riga.IndexOf("//", StringComparison.Ordinal);
        if (i >= 0) riga = riga[..i];
        i = riga.IndexOf('#');
        if (i >= 0) riga = riga[..i];
        return riga.Trim();
    }

    /// <summary>Angolo letto: il valore assoluto in gradi e l'emisfero se dichiarato (null = da dedurre).</summary>
    private readonly record struct Angolo(double Gradi, char? Emisfero, bool Negativo);

    private static (List<Angolo> Angoli, List<string> Etichette) LeggiRiga(string riga)
    {
        // Le forme a spazi e a due punti diventano simboliche PRIMA di spezzare: se si spezzasse per primo,
        // «41 59 26 N» diventerebbe quattro pezzi e nessuno di loro sarebbe una coordinata.
        riga = riga.ToUpperInvariant();
        riga = RxSpaziEmisferoDietro.Replace(riga, m => $"{m.Groups["d"].Value}°{m.Groups["m"].Value}'{m.Groups["s"].Value}\"{m.Groups["h"].Value}");
        riga = RxSpaziEmisferoDavanti.Replace(riga, m => $"{m.Groups["h"].Value}{m.Groups["d"].Value}°{m.Groups["m"].Value}'{m.Groups["s"].Value}\"");
        riga = RxDuePunti.Replace(riga, m => $"{m.Groups["d"].Value}°{m.Groups["m"].Value}'{m.Groups["s"].Value}\"");

        var angoli = new List<Angolo>();
        var etichette = new List<string>();

        foreach (var pezzo in riga.Split(Separatori, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = pezzo.Trim('"', '\'', '[', ']', '(', ')');
            if (token.Length == 0) continue;

            if (ProvaToken(token, angoli)) continue;
            etichette.Add(pezzo);
        }

        return (angoli, etichette);
    }

    /// <summary>Un pezzo di riga → uno o due angoli. False = non è una coordinata (allora è un'etichetta).</summary>
    private static bool ProvaToken(string token, List<Angolo> angoli)
    {
        if (RxAuroraPuntata.IsMatch(token) || RxAuroraCompatta.IsMatch(token))
        {
            if (!DmsCoordinate.TryParse(token, out var g)) return false;
            angoli.Add(new Angolo(Math.Abs(g), token[0], g < 0));
            return true;
        }

        var arinc = RxArinc.Match(token);
        if (!arinc.Success) arinc = RxArincEmisferoDavanti.Match(token);
        if (arinc.Success)
        {
            var ha = arinc.Groups["ha"].Value[0];
            var hb = arinc.Groups["hb"].Value[0];
            // Due emisferi dello stesso asse (N…N) non sono una coppia: è un'altra cosa, e non la si indovina.
            if (Asse(ha) == Asse(hb)) return false;
            if (!Impacchettato(arinc.Groups["a"].Value, out var va)) return false;
            if (!Impacchettato(arinc.Groups["b"].Value, out var vb)) return false;
            angoli.Add(new Angolo(va, ha, false));
            angoli.Add(new Angolo(vb, hb, false));
            return true;
        }

        var sim = RxSimboli.Match(token);
        if (sim.Success)
        {
            var d = double.Parse(sim.Groups["d"].Value, CultureInfo.InvariantCulture);
            var m = sim.Groups["m"].Success ? double.Parse(sim.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
            var s = sim.Groups["s"].Success ? double.Parse(sim.Groups["s"].Value, CultureInfo.InvariantCulture) : 0;
            angoli.Add(new Angolo(d + m / 60.0 + s / 3600.0, Emisfero(sim), false));
            return true;
        }

        var semp = RxSemplice.Match(token);
        if (semp.Success)
        {
            var testo = semp.Groups["v"].Value;
            var negativo = testo[0] == '-';
            if (!Impacchettato(testo.TrimStart('+', '-'), out var v)) return false;
            angoli.Add(new Angolo(v, Emisfero(semp), negativo));
            return true;
        }

        return false;
    }

    private static char? Emisfero(Match m) =>
        m.Groups["h1"].Success ? m.Groups["h1"].Value[0]
        : m.Groups["h2"].Success ? m.Groups["h2"].Value[0]
        : null;

    private static char Asse(char emisfero) => emisfero is 'N' or 'S' ? 'A' : 'O';

    /// <summary>
    /// Il numero nudo, letto <b>da destra</b> come lo scrivono i formati a larghezza fissa: fino a 3 cifre
    /// intere sono gradi decimali, 4-5 sono <c>DDMM.mmm</c>, 6 o più sono <c>DDMMSS.ss</c>.
    /// </summary>
    /// <remarks>⚠️ Non è un'euristica azzardata: un grado decimale non può avere quattro cifre intere (il
    /// massimo è 180), quindi il caso non si sovrappone mai a quello dei gradi.</remarks>
    private static bool Impacchettato(string testo, out double gradi)
    {
        gradi = 0;
        if (!double.TryParse(testo, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return false;
        var punto = testo.IndexOf('.');
        var cifreIntere = punto < 0 ? testo.Length : punto;

        if (cifreIntere <= 3) { gradi = v; return true; }

        if (cifreIntere <= 5)
        {
            var d = Math.Floor(v / 100);
            gradi = d + (v - d * 100) / 60.0;
            return true;
        }

        var dd = Math.Floor(v / 10000);
        var resto = v - dd * 10000;
        var mm = Math.Floor(resto / 100);
        gradi = dd + mm / 60.0 + (resto - mm * 100) / 3600.0;
        return true;
    }

    /// <summary>
    /// Due angoli → un punto. Gli emisferi dichiarati decidono; quando mancano vale la convenzione del testo
    /// (<b>latitudine prima</b>: DB IVAO, Google Maps, sectorfile), e se il primo numero non può essere una
    /// latitudine i due si scambiano — dichiarandolo.
    /// </summary>
    private static bool ProvaCoppia(Angolo a, Angolo b,
        out (double Lat, double Lon) punto, out CoordinateIssueKind? avviso)
    {
        punto = default;
        avviso = null;

        var assA = a.Emisfero is { } ea ? Asse(ea) : (char?)null;
        var assB = b.Emisfero is { } eb ? Asse(eb) : (char?)null;

        if (assA is not null && assA == assB) return false;   // N…N: non è una coppia

        var scambia =
            assA == 'O' || assB == 'A' ||
            (assA is null && assB is null && a.Gradi > 90 && b.Gradi <= 90);

        if (assA is null && assB is null && a.Gradi > 90 && b.Gradi <= 90)
            avviso = CoordinateIssueKind.LatLonScambiate;

        var lat = scambia ? b : a;
        var lon = scambia ? a : b;

        var vLat = Segno(lat);
        var vLon = Segno(lon);

        if (Math.Abs(vLat) > 90 || Math.Abs(vLon) > 180)
        {
            avviso = CoordinateIssueKind.FuoriIntervallo;
            return false;
        }

        punto = (vLat, vLon);
        return true;
    }

    private static double Segno(Angolo a) =>
        a.Negativo || a.Emisfero is 'S' or 'W' ? -a.Gradi : a.Gradi;
}
