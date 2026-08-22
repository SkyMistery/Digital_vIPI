using System.Globalization;
using Vipi.Application.Abstractions;

namespace Vipi.Infrastructure.Sectorfile;

/// <summary>
/// Parser puro (nessun I/O) del sectorfile Aurora della divisione IT: navaid (itvor/itndb/itfix) e SID per-aeroporto.
/// Formato SID (semicolon): <c>ICAO;pista[:pista…];CODICE;labelLat;labelLon;type;fixTransition;RNAV;</c>.
/// Il CODICE è <c>SID</c> o <c>SID-TRANS</c>; il fix di partenza è il prefisso troncato del codice (ultime 2
/// char = designatore cifra+lettera) da completare via navaid o alias.
/// </summary>
public static class AuroraSectorfileParser
{
    /// <summary>
    /// Il catalogo dei punti unendo itvor, itndb e itfix. Le coordinate non vengono parsate: né la completion
    /// dei fix SID né i suggerimenti dell'editor usano la posizione, e leggerle costerebbe 1400 conversioni DMS
    /// a ogni ciclo per un dato che nessuno guarda.
    /// </summary>
    /// <remarks>L'ordine di accodamento decide la natura di un nome presente in più file: VOR e NDB PRIMA dei
    /// fix, perché su un omonimo la radioassistenza è l'informazione più specifica delle due.</remarks>
    public static NavaidCatalog ParseNavaids(string? fixText, string? vorText, string? ndbText = null)
    {
        var entries = new List<NavaidName>();
        foreach (var name in ParseNavaidNames(vorText)) entries.Add(new NavaidName(name, NavaidKind.Vor));
        foreach (var name in ParseNavaidNames(ndbText)) entries.Add(new NavaidName(name, NavaidKind.Ndb));
        foreach (var name in ParseNavaidNames(fixText)) entries.Add(new NavaidName(name, NavaidKind.Fix));
        return new NavaidCatalog(entries);
    }

    private static IEnumerable<string> ParseNavaidNames(string? text)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            // I file navaid portano righe di commento in stile C ("//++++VOR ESTERNI++++", "//ESTERNI"):
            // non hanno il punto e virgola, quindi finivano nel catalogo INTERE, come se fossero nomi di
            // punto. Sulla completion delle SID non si vedeva — nessun prefisso di codice SID inizia per
            // barra — ma sono comparse in cima all'elenco a discesa dell'editor la prima volta che si è
            // aperto: e' cosi' che si e' visto un difetto che stava li' da sempre.
            if (line.StartsWith("//", StringComparison.Ordinal)) continue;
            var name = line.Split(';', 2)[0].Trim();
            if (name.Length != 0) yield return name;
        }
    }

    /// <summary>Parsa un file <c>&lt;icao&gt;.sid</c> in una lista di <see cref="SourceSid"/> risolti.</summary>
    public static IReadOnlyList<SourceSid> ParseSids(
        string icao, string? sidFile,
        IReadOnlySet<string> navNames,
        IReadOnlyDictionary<string, string> aliasMap)
    {
        var result = new List<SourceSid>();
        if (string.IsNullOrEmpty(sidFile)) return result;
        icao = icao.Trim().ToUpperInvariant();

        foreach (var raw in sidFile.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var c = line.Split(';');
            if (c.Length < 3) continue;

            var code = c[2].Trim();
            if (code.Length == 0) continue;
            var runwaysField = c[1].Trim();
            var transition = c.Length > 6 ? Blank(c[6]) : null;
            var rnav = c.Length > 7 && c[7].Trim() == "1";

            // Codice = SID o SID-TRANS: il fix di partenza si estrae dalla sola parte SID.
            var sidPart = code.Split('-')[0].Trim();
            var (prefix, letter) = SplitDesignator(sidPart);
            var (fix, needsReview) = ResolveFix(prefix, navNames, aliasMap);

            var runways = runwaysField.Length == 0
                ? new List<string?> { null }
                : runwaysField.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(r => (string?)r).ToList();
            if (runways.Count == 0) runways.Add(null);

            foreach (var rwy in runways)
            {
                var stableKey = string.Join('|', icao, fix.ToUpperInvariant(), letter.ToUpperInvariant(),
                    (transition ?? "").ToUpperInvariant(), (rwy ?? "").ToUpperInvariant());
                result.Add(new SourceSid(
                    Icao: icao, Runway: rwy, Fix: fix, Name: code, Transition: transition,
                    Type: rnav ? "RNAV" : "CONV", StableKey: stableKey, NeedsFixReview: needsReview));
            }
        }
        return result;
    }

    // Designatore = ultime 2 char (cifra+lettera); il resto è il prefisso fix troncato. La lettera è l'ultimo char.
    private static (string Prefix, string Letter) SplitDesignator(string sidCode)
    {
        if (sidCode.Length <= 2) return (sidCode, sidCode.Length > 0 ? sidCode[^1..] : "");
        return (sidCode[..^2], sidCode[^1..]);
    }

    // Risoluzione: match esatto → alias autoritativo → UNICO nome che inizia col prefisso → altrimenti (ambiguo o
    // nessuno) grezzo + NeedsFixReview. L'ambiguità (più candidati) NON si indovina: va risolta con un alias.
    private static (string Fix, bool NeedsReview) ResolveFix(
        string prefix,
        IReadOnlySet<string> navNames,
        IReadOnlyDictionary<string, string> aliasMap)
    {
        if (prefix.Length == 0) return (prefix, true);

        // (1) match esatto O(1) (il prefisso È già un fix/VOR, es. OST). Set case-insensitive: i nomi Aurora sono
        // maiuscoli come i codici SID, quindi il prefisso porta già la grafia canonica.
        if (navNames.Contains(prefix)) return (prefix, false);

        // (2) alias autoritativo (scavalca l'ambiguità).
        if (aliasMap.TryGetValue(prefix, out var aliased) && !string.IsNullOrWhiteSpace(aliased))
            return (aliased, false);

        // (3) UNICO nome che inizia col prefisso (il fix reale è più lungo del troncato). Se più di uno → ambiguo.
        string? only = null;
        var multiple = false;
        foreach (var name in navNames)
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (only is null) only = name;
                else { multiple = true; break; }
            }
        if (only is not null && !multiple) return (only, false);

        // (4) ambiguo o nessun match → da verificare a mano.
        return (prefix, true);
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // --- MRVA (ENRMVA/{acc}.mva e {icao}.mva) ---

    /// <summary>
    /// Parsa un file <c>.mva</c> (minime di vettoramento) in <see cref="MvaChart"/>: tracciati ed etichette,
    /// <b>verbatim</b>. Due tipi di riga, indipendenti fra loro:
    /// <list type="bullet">
    /// <item><c>L;nome;lat;lon;TESTO;colore;</c> — testo piazzato in un punto. Non è l'attributo quota di un'area:
    /// in <c>liph.mva</c> le dieci <c>L;</c> stanno tutte in cima al file, prima di qualsiasi vertice.</item>
    /// <item><c>T;gruppo;lat;lon;[extra];</c> — vertice. Il blocco chiude su riga vuota, su
    /// <c>T;DUMMY;N000…;E000…;</c> (che alza la penna e non è un vertice) o al cambio di nome gruppo.</item>
    /// </list>
    /// Niente viene interpretato: il testo dell'etichetta resta com'è (<c>110</c>, <c>1500</c>, <c>TRL</c>,
    /// <c>NO MINIMA</c>, <c>80/TRL</c> — nessun campo dice le unità), i tracciati aperti restano aperti, e
    /// l'associazione etichetta↔area non viene dedotta perché il formato non la dichiara. Puro, deterministico.
    /// </summary>
    public static MvaChart ParseMva(string? text)
    {
        if (string.IsNullOrEmpty(text)) return MvaChart.Empty;

        var labels = new List<MvaLabel>();
        var shapes = new List<MvaShape>();
        string? name = null;
        List<MvaPoint>? points = null;

        void Flush()
        {
            // Un vertice solo non è né linea né area: niente da disegnare.
            if (name is not null && points is { Count: >= 2 })
            {
                var first = points[0];
                var last = points[^1];
                var closed = first.Lat.Equals(last.Lat) && first.Lon.Equals(last.Lon);
                shapes.Add(new MvaShape(name, closed, points));
            }
            name = null; points = null;
        }

        foreach (var raw in text.Split('\n'))
        {
            // I file portano commenti a fine riga ("//START Circle", "//FL110") e interi poligoni commentati:
            // tolto il commento, quelle righe restano vuote e chiudono il blocco come una riga vuota qualsiasi.
            var line = raw;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) line = line[..comment];
            line = line.Trim();
            if (line.Length == 0) { Flush(); continue; }

            var f = line.Split(';');
            var tag = f[0].Trim().ToUpperInvariant();

            if (tag == "L" && f.Length >= 6)
            {
                Flush();   // una nuova etichetta apre un blocco: quello in corso è finito
                if (!TryParseMvaCoordinate(f[2], out var lat) || !TryParseMvaCoordinate(f[3], out var lon)) continue;
                labels.Add(new MvaLabel(f[4].Trim(), lat, lon, Blank(f[5])));
            }
            else if (tag == "T" && f.Length >= 4)
            {
                var group = f[1].Trim();
                if (group.Equals("DUMMY", StringComparison.OrdinalIgnoreCase)) { Flush(); continue; }
                if (!TryParseMvaCoordinate(f[2], out var lat) || !TryParseMvaCoordinate(f[3], out var lon)) continue;

                if (points is null) { name = group; points = new List<MvaPoint>(); }
                else if (!string.Equals(group, name, StringComparison.OrdinalIgnoreCase))
                {
                    // Gruppi consecutivi senza separatore (lirs.mva, libn.mva): li distingue solo il nome.
                    Flush();
                    name = group; points = new List<MvaPoint>();
                }
                points.Add(new MvaPoint(lat, lon));
            }
        }
        Flush();

        return labels.Count == 0 && shapes.Count == 0 ? MvaChart.Empty : new MvaChart(shapes, labels);
    }

    /// <summary>
    /// Coordinata di un file <c>.mva</c>: le due forme DMS di <see cref="TryParseDms"/> più i <b>gradi decimali
    /// puri</b> senza emisfero (<c>45.55756591</c>), che nel sectorfile compaiono in una riga sola —
    /// <c>lipx.mva</c> riga 14. È un'anomalia del dato, ma scartarla farebbe sparire un'etichetta in silenzio.
    /// <para>
    /// Senza la lettera, l'emisfero lo dice il <b>segno</b>, con la convenzione standard: sulla latitudine
    /// <c>+</c> = N e <c>-</c> = S, sulla longitudine <c>+</c> = E e <c>-</c> = W. È la stessa uscita che dà la
    /// forma con la lettera (dove <c>S</c> e <c>W</c> diventano negativi), quindi a valle non c'è differenza.
    /// </para>
    /// </summary>
    /// <remarks>Il ripiego vive qui e NON in <see cref="TryParseDms"/>: <see cref="ParseTowerShapes"/> usa il
    /// rifiuto di quel metodo per distinguere un vertice da un'intestazione di blocco, e accettare numeri nudi
    /// gliela romperebbe.</remarks>
    private static bool TryParseMvaCoordinate(string? token, out double degrees) =>
        TryParseDms(token, out degrees)
        || double.TryParse(token?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out degrees);

    // --- TWR shape (DYNAMIC_SEC/twrs.tfl) ---

    /// <summary>
    /// Parsa il file <c>twrs.tfl</c> (poligoni TWR di Aurora) in una mappa callsign → anello di punti (Lat, Lon).
    /// Formato a blocchi: riga intestazione <c>CALLSIGN;TWR;1;TWR;1;</c> seguita da righe coordinata
    /// <c>N041.37.28.965;E015.43.18.960;</c> (DMS, un vertice per riga), il blocco chiude su riga vuota o sull'header
    /// successivo. Anelli con &lt; 3 punti scartati. Puro, deterministico. Chiave callsign in MAIUSCOLO.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<(double Lat, double Lon)>> ParseTowerShapes(string? tfl)
    {
        var result = new Dictionary<string, IReadOnlyList<(double, double)>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(tfl)) return result;

        string? current = null;
        List<(double, double)>? ring = null;

        void Flush()
        {
            if (current is not null && ring is { Count: >= 3 }) result[current] = ring;
            current = null; ring = null;
        }

        foreach (var raw in tfl.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) { Flush(); continue; }   // riga vuota = fine blocco

            var fields = line.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (fields.Length == 2 && TryParseDms(fields[0], out var lat) && TryParseDms(fields[1], out var lon))
            {
                ring?.Add((lat, lon));   // vertice (ignorato se non siamo dentro un blocco)
            }
            else if (fields.Length >= 1 && fields[0].Length != 0)
            {
                Flush();                 // nuova intestazione: chiude il blocco precedente
                current = fields[0].ToUpperInvariant();
                ring = new List<(double, double)>();
            }
        }
        Flush();
        return result;
    }

    /// <summary>
    /// Converte una coordinata DMS Aurora in gradi decimali con segno (S/W negativi). Accetta <b>entrambe</b> le
    /// forme che convivono nel sectorfile italiano: quella coi punti (<c>N041.37.28.965</c>) e quella
    /// <b>compatta</b> (<c>N0463144000</c> = 046°31'44.000"), usata da <c>liph.mva</c>, <c>itgeo.geo</c> e dai
    /// <c>.vfi</c>. False se malformata.
    /// </summary>
    /// <remarks>La forma compatta si legge da destra: 3 cifre di millisecondi, 2 di secondi, 2 di primi, il resto
    /// gradi — così vale sia per la latitudine sia per la longitudine, che ha un grado in più.</remarks>
    public static bool TryParseDms(string? token, out double degrees)
    {
        degrees = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;
        token = token.Trim();
        var hemi = char.ToUpperInvariant(token[0]);
        if (hemi is not ('N' or 'S' or 'E' or 'W')) return false;

        var body = token[1..];
        if (!body.Contains('.')) return TryParseCompactDms(body, hemi, out degrees);

        var parts = body.Split('.');
        if (parts.Length < 3) return false;
        // Secondi = "SS.sss": parts[2] interi + eventuale parts[3] frazione.
        var secText = parts.Length >= 4 ? parts[2] + "." + parts[3] : parts[2];
        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var deg)) return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var min)) return false;
        if (!double.TryParse(secText, NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)) return false;

        var value = deg + min / 60.0 + sec / 3600.0;
        degrees = hemi is 'S' or 'W' ? -value : value;
        return true;
    }

    /// <summary>Forma compatta <c>DDD MM SS sss</c> senza separatori, letta da destra. Serve almeno una cifra di
    /// gradi oltre alle 7 fisse (3 millisecondi + 2 secondi + 2 primi).</summary>
    private static bool TryParseCompactDms(string body, char hemi, out double degrees)
    {
        degrees = 0;
        if (body.Length < 8) return false;
        foreach (var c in body) if (!char.IsAsciiDigit(c)) return false;

        var frac = body[^3..];
        var sec = body[^5..^3];
        var min = body[^7..^5];
        var deg = body[..^7];

        if (!int.TryParse(deg, NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)) return false;
        if (!int.TryParse(min, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)) return false;
        if (!double.TryParse(sec + "." + frac, NumberStyles.Float, CultureInfo.InvariantCulture, out var s)) return false;

        var value = d + m / 60.0 + s / 3600.0;
        degrees = hemi is 'S' or 'W' ? -value : value;
        return true;
    }
}
