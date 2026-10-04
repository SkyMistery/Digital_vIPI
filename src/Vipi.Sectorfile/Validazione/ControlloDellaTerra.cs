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
public static class ControlloDellaTerra
{
    private const double MetriDalloScalo = 10_000;
    private const double MetriDallaTaxiway = 100;

    // I tipi dei .geo che fanno da taxiway per un'etichetta: l'asse e il bordo.
    private static readonly string[] TipiDiTaxiway = ["TAXI_CENTER", "TAXIWAY"];

    /// <summary>
    /// I problemi della terra. <paramref name="file"/>: i <c>.txi</c>, <c>.gts</c>, <c>.geo</c> e <c>.pol</c> dell'albero
    /// col percorso da mostrare e i record; <paramref name="scali"/>: gli scali di tutti gli <c>.ap</c>;
    /// <paramref name="coloriDefiniti"/>: i nomi dei <c>.def</c>; <paramref name="testoDellaRiga"/>: la riga del disco.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<object> Record)> file,
                                                    IEnumerable<AirportInfo> scali, IReadOnlySet<string> coloriDefiniti,
                                                    Func<string, int, string> testoDellaRiga)
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

        foreach (var (relativo, record) in file)
        {
            string estensione = Estensione(relativo);
            string delFile = Path.GetFileNameWithoutExtension(relativo).ToUpperInvariant();
            switch (estensione)
            {
                case "gts" when arp.ContainsKey(delFile):
                {
                    var visti = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var stand in record.OfType<Stand>().Where(s => !s.IsDisabled))
                    {
                        int riga = stand.Source.LineNumber;
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
