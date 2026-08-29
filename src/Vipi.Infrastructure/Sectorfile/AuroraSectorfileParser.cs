using System.Globalization;
using Vipi.Application.Abstractions;
using Vipi.Application.Coordinates;

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
    /// Il catalogo dei punti unendo itvor, itndb e itfix, <b>con le coordinate</b>.
    /// </summary>
    /// <remarks>
    /// <para>L'ordine di accodamento decide la natura di un nome presente in più file: VOR e NDB PRIMA dei
    /// fix, perché su un omonimo la radioassistenza è l'informazione più specifica delle due.</para>
    /// <para>⚠️ Fino al 26 agosto 2026 le coordinate si saltavano di proposito («nessuno guarda la
    /// posizione»). Ora servono ai poligoni di settore, dove 233 vertici sono nomi di punto. Il costo è
    /// ~2500 conversioni DMS per giro d'import — una volta ogni 24 ore, su un catalogo tenuto in cache.</para>
    /// </remarks>
    public static NavaidCatalog ParseNavaids(string? fixText, string? vorText, string? ndbText = null) =>
        ParseNavaids(new[]
        {
            (NavaidKind.Vor, vorText),
            (NavaidKind.Ndb, ndbText),
            (NavaidKind.Fix, fixText),
        });

    /// <summary>
    /// Il catalogo da <b>quanti</b> file servono: i punti della divisione non stanno in tre file, ne stanno in
    /// otto (<c>ESTERNI.fix</c>, <c>MIL.fix</c>, <c>APT.fix</c>, <c>secsi.fix</c>… oltre ai tre principali), e
    /// quali siano lo dice <c>ITALY.isc</c>.
    ///
    /// <para>⚠️ <b>L'ordine conta</b>: a parità di nome vince la PRIMA occorrenza (regola di
    /// <see cref="NavaidCatalog"/>), e con essa la natura del punto. Il chiamante accoda VOR e NDB prima dei
    /// fix perché un omonimo dev'essere la radioassistenza, non il punto di riporto.</para>
    /// </summary>
    public static NavaidCatalog ParseNavaids(IEnumerable<(NavaidKind Kind, string? Text)> files)
    {
        var entries = new List<NavaidName>();
        foreach (var (kind, text) in files)
            foreach (var e in ParseNavaidEntries(text, kind))
                entries.Add(e);
        return new NavaidCatalog(entries);
    }

    private static IEnumerable<NavaidName> ParseNavaidEntries(string? text, NavaidKind kind)
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

            var fields = line.Split(';');
            var name = fields[0].Trim();
            if (name.Length == 0) continue;

            // ⚠️ La coppia di coordinate si CERCA, non si prende a indice fisso: i tre file la mettono in
            // colonne diverse — `ABADI;lat;lon;…` nei fix, `AEA;111.65;lat;lon;…` nei VOR e negli NDB, dove
            // c'è la frequenza in mezzo. Cercare la prima coppia consecutiva che si legge come DMS copre
            // tutti e tre senza tre regole da tenere allineate.
            double? lat = null, lon = null;
            for (var i = 1; i + 1 < fields.Length; i++)
                if (TryParseDms(fields[i], out var la) && TryParseDms(fields[i + 1], out var lo))
                {
                    (lat, lon) = (la, lo);
                    break;
                }

            yield return new NavaidName(name, kind, lat, lon);
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

    // --- Poligoni di SETTORE (DYNAMIC_SEC/*.tfl, CTR/APP/MIL/FSS) ---

    /// <summary>
    /// Cos'è uscito da un file di settore: gli anelli per callsign, e i nomi di punto che non si sono
    /// risolti. I secondi non sono un dettaglio da log — sono il motivo per cui un'area può mancare.
    /// </summary>
    /// <param name="Rings">Anello (Lat, Lon) per ogni callsign. Un anello può valere per PIÙ callsign.</param>
    /// <param name="UnresolvedPoints">I nomi che il catalogo non conosce, con i callsign che li citavano.</param>
    public sealed record SectorShapeParse(
        IReadOnlyDictionary<string, IReadOnlyList<(double Lat, double Lon)>> Rings,
        IReadOnlyList<(string Point, string Callsigns)> UnresolvedPoints);

    /// <summary>
    /// Parsa un file di settore Aurora (<c>DYNAMIC_SEC/*.tfl</c>: <c>lirr_ne_ctr.tfl</c>, <c>lirrapp.tfl</c>,
    /// <c>lirr_mil.tfl</c>…) in anelli per callsign. Stesso formato di <see cref="ParseTowerShapes"/>, con
    /// due differenze che <b>non</b> sono cosmetiche — misurate sui 112 blocchi veri del 26 agosto 2026:
    ///
    /// <para><b>1. Un'intestazione può portare più callsign</b>, separati da spazio:
    /// <c>LIBB_ES_CTR LIBB_EU_CTR;CTR;1;CTR;1;</c>, fino a cinque
    /// (<c>EDMM_CTR EDMM_S_CTR EDMM_FSS EDMM_MIL_CTR</c>). È una shape che serve più enti, e l'anello si
    /// registra per ognuno. <see cref="ParseTowerShapes"/> ne farebbe una chiave sola, che non combacia con
    /// niente.</para>
    ///
    /// <para><b>2. Un vertice può essere un NOME di punto</b> invece di una coordinata: <c>TUFTE;TUFTE;</c>
    /// — 233 righe su 20 692. Si risolvono col catalogo navaid. Per <see cref="ParseTowerShapes"/> quella
    /// riga è un'intestazione nuova, quindi l'anello si spezza in frammenti, in silenzio.</para>
    ///
    /// <para>⚠️ <b>Un punto che non si risolve invalida l'anello INTERO.</b> Saltarlo non darebbe un poligono
    /// più piccolo: ne darebbe uno <b>sbagliato</b>, con un lato che taglia dritto dove il confine gira — e
    /// si disegna benissimo, quindi nessuno se ne accorge. Il blocco si scarta e il nome finisce in
    /// <see cref="SectorShapeParse.UnresolvedPoints"/>.</para>
    ///
    /// <para>Anelli con meno di 3 punti scartati. Puro e deterministico. Chiavi in MAIUSCOLO.</para>
    /// </summary>
    /// <param name="points">Il catalogo per risolvere i nomi. Vuoto = i blocchi con nomi si scartano tutti.</param>
    /// <summary>
    /// Come si separano più callsign in un'intestazione. ⚠️ Sono <b>due</b>, e la seconda è saltata fuori solo
    /// provando il parser sui file veri: 16 intestazioni usano lo spazio (<c>DAAA_CTR DAAA_NE_CTR</c>) e 3 i
    /// due punti (<c>LIMM_WS2_CTR:LIMM_WS5_CTR:LIMM_ES2_CTR:LIMM_ES5_CTR</c>). Leggendo solo lo spazio, quelle
    /// tre davano una chiave sola coi due punti dentro, che non combacia con nessun settore — quattro settori
    /// di Milano senza area, in silenzio.
    /// </summary>
    private static readonly char[] CallsignSeparators = { ' ', ':' };

    public static SectorShapeParse ParseSectorShapes(string? tfl, NavaidCatalog points)
    {
        var rings = new Dictionary<string, IReadOnlyList<(double, double)>>(StringComparer.OrdinalIgnoreCase);
        var irrisolti = new List<(string, string)>();
        if (string.IsNullOrEmpty(tfl)) return new SectorShapeParse(rings, irrisolti);

        string[]? callsigns = null;
        List<(double, double)>? ring = null;
        string? mancante = null;

        void Flush()
        {
            if (callsigns is { Length: > 0 })
            {
                if (mancante is not null) irrisolti.Add((mancante, string.Join(" ", callsigns)));
                else if (ring is { Count: >= 3 })
                    foreach (var cs in callsigns) rings[cs] = ring;
            }
            callsigns = null; ring = null; mancante = null;
        }

        foreach (var raw in tfl.Split('\n'))
        {
            // I file di settore commentano a fine riga: `LIRR_NE_CTR;CTR;1;CTR;1; //NE cnf.1`.
            var line = raw.Split("//", 2, StringSplitOptions.None)[0].Trim();
            if (line.Length == 0) { Flush(); continue; }

            var fields = line.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Vertice in coordinate.
            if (fields.Length == 2 && TryParseDms(fields[0], out var lat) && TryParseDms(fields[1], out var lon))
            {
                ring?.Add((lat, lon));
                continue;
            }

            // Vertice per NOME: due campi non-DMS uguali fra loro. L'uguaglianza è la firma della forma
            // (`AMSOR;AMSOR;` su tutte e 233 le righe misurate) e distingue il vertice da un'intestazione
            // malformata senza doverla indovinare.
            if (fields.Length == 2
                && string.Equals(fields[0], fields[1], StringComparison.OrdinalIgnoreCase))
            {
                if (ring is null) continue;                       // fuori da un blocco: niente da fare
                if (points.TryGetPoint(fields[0], out var p)) ring.Add((p.Lat, p.Lon));
                else mancante ??= fields[0].ToUpperInvariant();   // il PRIMO che manca: basta lui a invalidare
                continue;
            }

            // Tutto il resto è un'intestazione: chiude il blocco precedente e ne apre uno.
            if (fields.Length >= 1 && fields[0].Length != 0)
            {
                Flush();
                callsigns = fields[0].ToUpperInvariant()
                    .Split(CallsignSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                ring = new List<(double, double)>();
            }
        }
        Flush();

        return new SectorShapeParse(rings, irrisolti);
    }

    /// <summary>
    /// Anello (Lat, Lon) → JSON <c>[[lng,lat],…]</c>: <b>longitudine prima</b>, che è la forma di
    /// <c>RegionMapPolygon</c> di IVAO. Sta qui e non nei due provider perché l'ordine invertito è una
    /// conoscenza del formato, e scritta in due posti prima o poi diverge in uno solo.
    /// </summary>
    /// <remarks>⚠️ Come per il DMS, la scrittura vive in <c>Vipi.Application</c>
    /// (<see cref="AuroraRingJson"/>): dal 29 agosto 2026 la usa anche il convertitore di coordinate, che
    /// l'infrastruttura non la vede. Questa firma resta per i suoi chiamanti: è una delega.</remarks>
    public static string RingToPolygonJson(IReadOnlyList<(double Lat, double Lon)> ring) =>
        AuroraRingJson.Scrivi(ring);

    /// <summary>
    /// Converte una coordinata DMS Aurora in gradi decimali con segno (S/W negativi). Accetta <b>entrambe</b> le
    /// forme che convivono nel sectorfile italiano: quella coi punti (<c>N041.37.28.965</c>) e quella
    /// <b>compatta</b> (<c>N0463144000</c> = 046°31'44.000"), usata da <c>liph.mva</c>, <c>itgeo.geo</c> e dai
    /// <c>.vfi</c>. False se malformata.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Il formato non si legge qui</b>: dal 29 agosto 2026 la conoscenza del DMS Aurora sta in
    /// <see cref="DmsCoordinate"/> (in <c>Vipi.Application</c>), perché la usa anche il convertitore di
    /// coordinate, che l'infrastruttura non la vede. Questa firma resta per i suoi chiamanti e per i suoi
    /// test: è una delega, non una seconda implementazione.
    /// </remarks>
    public static bool TryParseDms(string? token, out double degrees) =>
        DmsCoordinate.TryParse(token, out degrees);
}
