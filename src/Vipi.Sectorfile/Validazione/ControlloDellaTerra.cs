using System.Globalization;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// I controlli della terra (lotto «Subito» slice 12a, «file per file» H2, I4, R1, R3): stand ed etichette con l'ICAO di un
/// altro scalo, lontani dallo scalo, ripetuti; l'etichetta lontana dalla sua taxiway; il tipo vuoto o sconosciuto di un
/// segmento <c>.geo</c> e il colore sconosciuto di un <c>.pol</c>; il <c>.pol</c> di uno scalo senza il suo <c>.geo</c>.
/// </summary>
/// <remarks>
/// Misure sul fork del 4 ottobre 2026 (<c>8cf32c6</c>). Stand: 1 672, a una mediana di 750 m dall'ARP e mai oltre 2,4 km,
/// tranne quello di <c>LIBP</c> in <c>libg.gts</c> (344 km); le etichette di <c>LIRF</c> arrivano a 5,3 km (la soglia della
/// 16L) → 10 km: uno scalo non è più grande. Etichette: 1 075, a una mediana di 1 m da un asse o da
/// un bordo di taxiway del <c>.geo</c> dello scalo, il 90% entro 37 m, 27 oltre 100 m (contro il solo asse sarebbero 71:
/// non tutte le taxiway hanno l'asse disegnato) → 100 m. Tipi dei <c>.geo</c>: 10 vuoti in <c>liap.geo</c>, nessuno
/// sconosciuto. Colori dei <c>.pol</c>: tutti in <c>colors.def</c>. Ogni <c>.pol</c> ha il <c>.geo</c> del suo scalo.
/// Soglie scelte dall'agente dalla misura.
/// </remarks>
public static partial class ControlloDellaTerra
{
    private const double MetriDalloScalo = 10_000;
    private const double MetriDallaTaxiway = 100;

    // «La taxiway che porta allo stand» (R6) è l'etichetta col codice più vicina, entro 300 m: scelta dell'agente — il
    // sector non dice quale taxiway serve uno stand, e sul fork i tag `code` sono ancora zero.
    private const double MetriDallaTaxiwayDelloStand = 300;

    // I tipi dei .geo che fanno da taxiway per un'etichetta: l'asse e il bordo.
    private static readonly string[] TipiDiTaxiway = ["TAXI_CENTER", "TAXIWAY"];

    /// <summary>
    /// I problemi della terra. <paramref name="file"/>: i <c>.txi</c>, <c>.gts</c>, <c>.geo</c> e <c>.pol</c> dell'albero
    /// col percorso da mostrare e i record; <paramref name="scali"/>: gli scali di tutti gli <c>.ap</c>;
    /// <paramref name="coloriDefiniti"/>: i nomi dei <c>.def</c>; <paramref name="testoDellaRiga"/>: la riga del disco.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<object> Record)> file,
                                                    IEnumerable<AirportInfo> scali, IReadOnlySet<string> coloriDefiniti,
                                                    Func<string, int, string> testoDellaRiga,
                                                    Func<object, IReadOnlyDictionary<string, string>?>? chiaviDi = null,
                                                    IEnumerable<Runway>? piste = null,
                                                    Func<string, IReadOnlyList<string>>? righeDi = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(scali);
        ArgumentNullException.ThrowIfNull(coloriDefiniti);
        ArgumentNullException.ThrowIfNull(testoDellaRiga);

        // La prima copia di uno scalo vale (i gemelli delle FIR sono uguali, e se no lo dice CopieDiverse).
        var arp = new Dictionary<string, Coordinate>(StringComparer.OrdinalIgnoreCase);
        foreach (var scalo in scali)
            arp.TryAdd(scalo.IcaoCode.Trim(), scalo.Centre);

        // Il .geo di uno scalo è quello che porta il suo nome (`lirf.geo`): i suoi assi e bordi di taxiway.
        var disegni = file.Where(f => Estensione(f.Relativo) == "geo")
            .GroupBy(f => Path.GetFileNameWithoutExtension(f.Relativo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Record, StringComparer.OrdinalIgnoreCase);

        // I versi di pista di ogni scalo, per le marcature (slice 12d, O3).
        var versi = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pista in piste ?? [])
        {
            if (!versi.TryGetValue(pista.IcaoCode.Trim(), out var suoi))
                versi[pista.IcaoCode.Trim()] = suoi = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            suoi.Add(pista.Designator1.Trim());
            suoi.Add(pista.Designator2.Trim());
        }

        var etichette = file.Where(f => Estensione(f.Relativo) == "txi")
            .GroupBy(f => Path.GetFileNameWithoutExtension(f.Relativo), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Record, StringComparer.OrdinalIgnoreCase);

        foreach (var (relativo, record) in file)
        {
            string estensione = Estensione(relativo);
            string delFile = Path.GetFileNameWithoutExtension(relativo).ToUpperInvariant();
            switch (estensione)
            {
                case "gts" when arp.ContainsKey(delFile):
                {
                    var visti = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    // Le etichette di taxiway dello scalo che dicono il loro codice massimo (R6).
                    var conCodice = (etichette.GetValueOrDefault(delFile) ?? []).OfType<TaxiwayLabel>()
                        .Select(t => (Etichetta: t, Codice: Codice(chiaviDi?.Invoke(t)))).Where(t => t.Codice is not null).ToList();
                    foreach (var stand in record.OfType<Stand>().Where(s => !s.IsDisabled))
                    {
                        int riga = stand.Source.LineNumber;
                        foreach (string fuori in FuoriDalManuale(stand))
                            yield return new(Regola.ValoreFuoriElenco, relativo, riga, testoDellaRiga(relativo, riga), fuori);
                        if (Codice(chiaviDi?.Invoke(stand)) is { } delloStand && conCodice.Count > 0)
                        {
                            var (vicina, dellaTaxiway) = conCodice.MinBy(t => Validatore.Metri(t.Etichetta.Position, stand.Position));
                            double metri = Validatore.Metri(vicina.Position, stand.Position);
                            if (metri <= MetriDallaTaxiwayDelloStand && delloStand > dellaTaxiway)
                            {
                                yield return new(Regola.StandPiuGrandeDellaTaxiway, relativo, riga, testoDellaRiga(relativo, riga),
                                    $"lo stand «{stand.Number.Trim()}» è di codice {delloStand}, la taxiway «{vicina.Name.Trim()}» che ha accanto ({Tondo(metri)} m) arriva al {dellaTaxiway}");
                            }
                        }

                        if (DelloScalo(relativo, riga, "lo stand", stand.Number, stand.IcaoCode, stand.Position, delFile, arp[delFile], testoDellaRiga) is { } problema)
                            yield return problema;
                        if (!visti.TryAdd(stand.Number.Trim(), riga))
                        {
                            yield return new(Regola.StandRipetuto, relativo, riga, testoDellaRiga(relativo, riga),
                                $"lo stand «{stand.Number.Trim()}» c'è già alla riga {visti[stand.Number.Trim()]}");
                        }
                    }

                    break;
                }

                case "txi" when arp.ContainsKey(delFile):
                {
                    var taxiway = (disegni.GetValueOrDefault(delFile) ?? []).OfType<Line>()
                        .Where(l => TipiDiTaxiway.Contains(l.Color.Trim(), StringComparer.OrdinalIgnoreCase)).ToList();
                    foreach (var etichetta in record.OfType<TaxiwayLabel>())
                    {
                        int riga = etichetta.Source.LineNumber;
                        if (DelloScalo(relativo, riga, "l'etichetta", etichetta.Name, etichetta.IcaoCode, etichetta.Position, delFile, arp[delFile], testoDellaRiga) is { } problema)
                        {
                            yield return problema;
                            continue;
                        }

                        // Uno scalo senza taxiway disegnate non ha niente a cui misurare l'etichetta.
                        if (taxiway.Count == 0)
                            continue;
                        double metri = taxiway.Min(l => MetriDalSegmento(etichetta.Position, l.Start, l.End));
                        if (metri > MetriDallaTaxiway)
                        {
                            yield return new(Regola.EtichettaLontanaDallaTaxiway, relativo, riga, testoDellaRiga(relativo, riga),
                                $"l'etichetta «{etichetta.Name.Trim()}» è a {Tondo(metri)} m dalla taxiway più vicina di {delFile.ToLowerInvariant()}.geo (asse o bordo)");
                        }
                    }

                    break;
                }

                case "geo" or "danger" or "restrict" or "prohibit":
                    foreach (var linea in record.OfType<Line>())
                    {
                        string tipo = linea.Color.Trim();
                        if (tipo.Length == 0)
                        {
                            yield return new(Regola.TipoSconosciuto, relativo, linea.Source.LineNumber, testoDellaRiga(relativo, linea.Source.LineNumber),
                                "tipo vuoto (5° campo): Aurora non sa in che strato e di che colore disegnarlo");
                        }
                        else if (!Noto(tipo, coloriDefiniti, delGeo: true))
                        {
                            yield return new(Regola.TipoSconosciuto, relativo, linea.Source.LineNumber, testoDellaRiga(relativo, linea.Source.LineNumber),
                                $"tipo «{tipo}» (5° campo): non è un tipo dei .geo, né un nome di colors.def, né un colore");
                        }
                    }

                    // Slice 12d (O3): i commenti delle marcature che nominano una pista che il .rw dello scalo non ha
                    // (rinumerata, o chiusa e tolta dal .rw come la 05/23 di LIBR).
                    if (estensione == "geo" && righeDi is not null && ScaloDelDisegno(delFile, record, arp) is { } scaloDelGeo
                        && versi.TryGetValue(scaloDelGeo, out var suoiVersi))
                    {
                        var righe = righeDi(relativo);
                        for (int i = 0; i < righe.Count; i++)
                        {
                            string riga = righe[i].TrimStart();
                            if (!riga.StartsWith("//", StringComparison.Ordinal))
                                continue;
                            var assenti = PisteCitate(riga).Where(v => !Ha(suoiVersi, v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                            if (assenti.Count > 0)
                            {
                                yield return new(Regola.MarcaturaDiUnaPistaAssente, relativo, i + 1, righe[i],
                                    $"il commento nomina la pista {string.Join(" e ", assenti)}, e il .rw dà a {scaloDelGeo.ToUpperInvariant()} " +
                                    $"{string.Join(", ", suoiVersi.Where(v => v.Length > 0).Order(StringComparer.Ordinal))}: pista rinumerata, o chiusa e tolta dal .rw");
                            }
                        }
                    }

                    break;

                case "pol":
                {
                    var poligoni = record.OfType<Polygon>().ToList();
                    foreach (var poligono in poligoni)
                    {
                        var ignoti = new[] { poligono.FillColor.Trim(), poligono.LineColor.Trim() }
                            .Where(c => !Noto(c, coloriDefiniti, delGeo: false)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        if (ignoti.Count > 0)
                        {
                            int riga = poligono.Source.LineNumber;
                            yield return new(Regola.TipoSconosciuto, relativo, riga, testoDellaRiga(relativo, riga),
                                $"colore «{string.Join("», «", ignoti.Select(c => c.Length == 0 ? "vuoto" : c))}»: non è un nome di colors.def né un colore");
                        }
                    }

                    // Slice 12d (I3): l'ordine di disegno. Un riempimento scritto dopo uno che gli sta sopra lo copre.
                    int piuAlto = -1;
                    string? chiE = null;
                    int doveE = 0;
                    foreach (var poligono in poligoni)
                    {
                        if (OrdineDeiRiempimenti.Posto(poligono.FillColor) is not { } posto)
                            continue;
                        if (posto < piuAlto)
                        {
                            int riga = poligono.Source.LineNumber;
                            yield return new(Regola.OrdineDiDisegno, relativo, riga, testoDellaRiga(relativo, riga),
                                $"{poligono.FillColor.Trim().ToUpperInvariant()} scritto dopo {chiE} (riga {doveE}): in Aurora vince l'ultimo del file, " +
                                $"e questo copre quello — l'ordine è {OrdineDeiRiempimenti.InParole}");
                        }
                        else
                        {
                            piuAlto = posto;
                            chiE = poligono.FillColor.Trim().ToUpperInvariant();
                            doveE = poligono.Source.LineNumber;
                        }
                    }

                    // Lo scalo di un .pol si riconosce da dove sta (`rf_ad_gnd.pol`, `lsza_ad_gnd.pol`: dal nome non si sa).
                    if (poligoni.FirstOrDefault(p => p.Vertices.Count > 0) is { } primo && arp.Count > 0)
                    {
                        // Più scali possono avere lo stesso centro (`LIMM`, «Milano Area», sta su LIMC): basta che uno abbia il .geo.
                        var vicini = arp.Select(a => (Scalo: a.Key, Metri: Validatore.Metri(a.Value, primo.Vertices[0])))
                            .Where(a => a.Metri <= MetriDalloScalo).OrderBy(a => a.Metri).Select(a => a.Scalo).ToList();
                        if (vicini.Count > 0 && !vicini.Any(disegni.ContainsKey))
                        {
                            int riga = primo.Source.LineNumber;
                            yield return new(Regola.RiempimentoSenzaDisegno, relativo, riga, testoDellaRiga(relativo, riga),
                                $"i riempimenti di {vicini[0].ToUpperInvariant()}, e nessun {vicini[0].ToLowerInvariant()}.geo coi suoi bordi");
                        }
                    }

                    break;
                }
            }
        }
    }

    // L'ICAO di un altro scalo nel file di questo, o il punto lontano dal suo scalo.
    private static ProblemaDelSector? DelloScalo(string relativo, int riga, string cosa, string nome, string icao, Coordinate dove,
                                                 string delFile, Coordinate arp, Func<string, int, string> testoDellaRiga)
    {
        double metri = Validatore.Metri(arp, dove);
        if (!string.Equals(icao.Trim(), delFile, StringComparison.OrdinalIgnoreCase))
        {
            string testo = testoDellaRiga(relativo, riga);
            // Dentro lo scalo è un refuso (L3MC, LINB): si propone l'ICAO del file. Fuori, è nel file sbagliato.
            return metri <= MetriDalloScalo
                ? new(Regola.ScaloDiversoDalFile, relativo, riga, testo,
                    $"{cosa} «{nome.Trim()}» dice {icao.Trim()}, il file è di {delFile}: Aurora non lo mette a {delFile}", ConLIcao(testo, delFile))
                : new(Regola.ScaloDiversoDalFile, relativo, riga, testo,
                    $"{cosa} «{nome.Trim()}» dice {icao.Trim()} e sta a {Tondo(metri / 1000)} km da {delFile}: è nel file sbagliato");
        }

        return metri > MetriDalloScalo
            ? new(Regola.LontanoDalloScalo, relativo, riga, testoDellaRiga(relativo, riga),
                $"{cosa} «{nome.Trim()}» è a {Tondo(metri / 1000)} km dal centro di {delFile}")
            : null;
    }

    // Lo scalo di un .geo: quello del suo nome (`lirf.geo`), o il più vicino al suo primo punto (`br_mark.geo`).
    private static string? ScaloDelDisegno(string delFile, IReadOnlyList<object> record, Dictionary<string, Coordinate> arp)
    {
        if (arp.ContainsKey(delFile))
            return delFile;
        if (record.OfType<Line>().FirstOrDefault() is not { } prima || arp.Count == 0)
            return null;
        var (scalo, metri) = arp.Select(a => (a.Key, Metri: Validatore.Metri(a.Value, prima.Start))).MinBy(a => a.Metri);
        return metri <= MetriDalloScalo ? scalo : null;
    }

    /// <summary>
    /// I versi di pista nominati da un commento di marcature: <c>//designator rw 05</c>, <c>//Runway 04R designator</c>,
    /// <c>//rwy 18/36</c>. Solo dopo «rw», «rwy» o «runway»: un <c>//2</c> o un <c>//Taxiway 12</c> non sono piste.
    /// </summary>
    public static IEnumerable<string> PisteCitate(string commento)
    {
        ArgumentNullException.ThrowIfNull(commento);
        foreach (System.Text.RegularExpressions.Match m in PistaNelCommento().Matches(commento))
        {
            yield return m.Groups[1].Value.ToUpperInvariant();
            if (m.Groups[2].Success)
                yield return m.Groups[2].Value.ToUpperInvariant();
        }
    }

    // «rw 11» a uno scalo con 11L e 11R nomina tutte e due: non è una pista che manca.
    private static bool Ha(HashSet<string> versi, string citato)
        => versi.Contains(citato) || (char.IsAsciiDigit(citato[^1]) && versi.Any(v => v.Length == 3 && v.StartsWith(citato, StringComparison.OrdinalIgnoreCase)));

    [System.Text.RegularExpressions.GeneratedRegex(@"\b(?:rw|rwy|runway)\s*(\d\d[LRC]?)\b(?:\s*[-/]\s*(\d\d[LRC]?)\b)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex PistaNelCommento();

    // Nome, tipo e slot di uno stand contro il manuale ([GATES], «Slots for Gates»).
    private static IEnumerable<string> FuoriDalManuale(Stand stand)
    {
        if (stand.Number.Trim().Length > 20)
            yield return $"il nome dello stand ha {stand.Number.Trim().Length} caratteri: al massimo 20";
        if (stand.Type is { } tipo && (tipo.Length != 1 || !SlotDelloStand.Tipi.Contains(char.ToUpperInvariant(tipo[0]), StringComparison.Ordinal)))
            yield return $"tipo «{tipo}» (5° campo): si scrive L, M, H, S o G";
        if (SlotDelloStand.Problemi(stand.Slot) is { Count: > 0 } slot)
            yield return "slot (6° campo): " + string.Join("; ", slot);
    }

    // Il codice ICAO (A-F) scritto nel tag `code` di uno stand o di una taxiway; null se non c'è o non è una lettera A-F.
    private static char? Codice(IReadOnlyDictionary<string, string>? chiavi)
        => chiavi is not null && chiavi.TryGetValue("code", out string? scritto) && IO.Metadati.Testo(scritto).Trim() is { Length: 1 } lettera
           && char.ToUpperInvariant(lettera[0]) is >= 'A' and <= 'F' and var codice
            ? codice
            : null;

    // La riga con l'ICAO del file nel 2° campo; null se la riga non ha la forma attesa.
    private static string? ConLIcao(string riga, string icao)
    {
        string[] campi = riga.Split(';');
        if (campi.Length < 4)
            return null;
        campi[1] = icao;
        return string.Join(';', campi);
    }

    private static bool Noto(string nome, IReadOnlySet<string> definiti, bool delGeo)
        => nome.Length > 0 && (definiti.Contains(nome) || (delGeo && NomiDeiColoriDelGeo.Chiave(nome) is not null)
                               || ColoreDelSector.TryLeggi(nome, out _));

    private static string Estensione(string relativo) => Path.GetExtension(relativo).TrimStart('.').ToLowerInvariant();

    private static string Tondo(double valore) => Math.Round(valore).ToString("0", CultureInfo.InvariantCulture);

    // Distanza in metri di un punto da un segmento, sul piano attorno al punto: basta per poche centinaia di metri.
    internal static double MetriDalSegmento(Coordinate p, Coordinate a, Coordinate b)
    {
        double cos = Math.Cos(p.LatitudeDeg * Math.PI / 180);
        double ax = (a.LongitudeDeg - p.LongitudeDeg) * 111_320 * cos, ay = (a.LatitudeDeg - p.LatitudeDeg) * 111_320;
        double bx = (b.LongitudeDeg - p.LongitudeDeg) * 111_320 * cos, by = (b.LatitudeDeg - p.LatitudeDeg) * 111_320;
        double dx = bx - ax, dy = by - ay, lungo = (dx * dx) + (dy * dy);
        double t = lungo == 0 ? 0 : Math.Clamp(-((ax * dx) + (ay * dy)) / lungo, 0, 1);
        double x = ax + (t * dx), y = ay + (t * dy);
        return Math.Sqrt((x * x) + (y * y));
    }
}
