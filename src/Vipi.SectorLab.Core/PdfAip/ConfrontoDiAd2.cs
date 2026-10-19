using System.Globalization;
using System.Text.RegularExpressions;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;
using PistaDellAip = Aip.Runway;
using PistaDelSector = Vipi.Sectorfile.Models.Runway;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Le tre parti del confronto di AD 2 (carta F6, slice 5b).</summary>
public enum ParteDiAd2
{
    /// <summary>Lo scalo dei <c>.ap</c>: punto di riferimento, elevazione, altitudine di transizione.</summary>
    Scali,

    /// <summary>Le piste dei <c>.rw</c>: soglie, elevazioni delle soglie, rotte magnetiche.</summary>
    Piste,

    /// <summary>Le frequenze di AD 2.18 contro le posizioni dei <c>.frq</c>.</summary>
    Frequenze,
}

/// <summary>
/// Una riga del confronto di AD 2 (slice 5b): uno scalo, una pista o una frequenza, com'è rispetto al sector, con le righe
/// che dicono le differenze. <paramref name="File"/>/<paramref name="Record"/>: la voce del sector da aprire (la chiave del
/// file nella sessione). Una voce che sta in più file (<c>itap.ap</c> e i <c>.ap</c> delle FIR) è una riga per file: le FIR
/// a volte non sono uguali a <c>OTHER/it…</c> (misura del 19 ottobre: LIRN in <c>libb.rw</c>).
/// </summary>
public sealed record DifferenzaDiAd2(EsitoDelConfronto Esito, string Scalo, string Nome, string? File, int? Record, IReadOnlyList<string> Dettagli);

/// <summary>
/// Il confronto del testo di AD 2 col sector, in sola lettura (carta F6, slice 5b). Si guardano solo gli scali che l'AIP
/// espanso ha (i PDF di AD 2 letti): degli altri il sector non ha niente da confrontare.
/// <para><b>Gli scali</b> (AD 2.2) coi <c>.ap</c>: il punto di riferimento oltre 0,1 NM (D4), l'elevazione (al piede),
/// l'altitudine di transizione (AD 2.17) dove il sector la scrive.</para>
/// <para><b>Le piste</b> (AD 2.12) coi <c>.rw</c>: per designatore, una riga per coppia di testate; la soglia oltre
/// <see cref="SogliaDellaSogliaM"/>, l'elevazione della soglia (al piede), la rotta del sector — che è magnetica — contro la
/// vera dell'AIP meno la declinazione di AD 2.2 com'è stampata, oltre <see cref="SogliaDellaRotta"/>.</para>
/// <para><b>Le frequenze</b> (AD 2.18) con le posizioni dei <c>.frq</c> che cominciano con l'indicativo dello scalo
/// (<c>LICA_TWR</c>); una frequenza di avvicinamento può stare in una posizione di un altro scalo (<c>LIMC_ANE_APP</c> per
/// Orio). Emergenza e ATIS il sector non le tiene nei <c>.frq</c>: non si confrontano.</para>
/// </summary>
public static partial class ConfrontoDiAd2
{
    /// <summary>
    /// La soglia di una pista oltre questi metri è diversa: le soglie del sector che tornano stanno sotto il metro (misura del
    /// 19 ottobre), quella della 10 di LICA a un centinaio. Proposta dell'agente, confermata dal committente (19 ottobre).
    /// </summary>
    public const double SogliaDellaSogliaM = 10;

    /// <summary>La rotta magnetica oltre questi gradi è diversa (confermato dal committente, 19 ottobre).</summary>
    public const double SogliaDellaRotta = 1;

    /// <summary>I servizi di AD 2.18 che il sector non tiene nei <c>.frq</c>.</summary>
    public static readonly IReadOnlyList<string> ServiziFuoriDaiFrq = ["Emergenza", "ATIS"];

    private const double MetriPerMiglio = 1852;

    private sealed record Voce<T>(string Relativo, string File, int Record, T Dato);

    /// <summary>La parte chiesta, dal modello dell'AIP espanso.</summary>
    public static IReadOnlyList<DifferenzaDiAd2> Di(ParteDiAd2 parte, AipDocument aip, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(aip);
        var scali = aip.Aerodromes ?? [];
        return parte switch
        {
            ParteDiAd2.Scali => Scali(scali, aip.Airspaces, sessione),
            ParteDiAd2.Piste => Piste(scali, sessione),
            _ => Frequenze(scali, sessione),
        };
    }

    // --- gli scali ---------------------------------------------------------------------------------------------------

    public static IReadOnlyList<DifferenzaDiAd2> Scali(IEnumerable<Aerodrome> scali, IEnumerable<Airspace> spazi, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(scali);
        ArgumentNullException.ThrowIfNull(spazi);
        ArgumentNullException.ThrowIfNull(sessione);
        var ap = Voci<AirportInfo>(sessione, ".ap").ToLookup(v => v.Dato.IcaoCode.Trim(), StringComparer.OrdinalIgnoreCase);
        var transizioni = AltitudiniDiTransizione(spazi);

        var esiti = new List<DifferenzaDiAd2>();
        foreach (var s in scali.OrderBy(s => s.Icao, StringComparer.OrdinalIgnoreCase))
        {
            var transizione = transizioni.GetValueOrDefault(s.Icao);
            var aip = $"AIP: {(s.Arp is { } arp ? Coordinate(arp) : "—")}, elevazione {s.Elevation ?? "—"}, transizione {transizione?.Testo ?? "—"}";
            var suoi = ap[s.Icao].ToList();
            if (suoi.Count == 0)
            {
                esiti.Add(new DifferenzaDiAd2(EsitoDelConfronto.MancaNelSector, s.Icao, $"{s.Icao} {s.Name}", null, null, [aip]));
                continue;
            }
            foreach (var v in suoi)
            {
                var d = v.Dato;
                var dettagli = new List<string> { aip, $"sector ({v.File}): {Coordinate(d.Centre)}, elevazione {d.ElevationFt} FT, transizione {(d.TransitionAltFt > 0 ? $"{d.TransitionAltFt} FT" : "—")}{(d.IsDisabled ? " (commentata)" : "")}" };
                var diversa = false;
                if (s.Arp is { } p && Distanza(p, d.Centre) is var scarto && scarto > ConfrontoDiEnr3.Soglia)
                {
                    diversa = true;
                    dettagli.Add($"punto di riferimento a {scarto.ToString("0.00", CultureInfo.InvariantCulture)} NM da quello dell'AIP");
                }
                if (Piedi(s.Elevation) is { } e && Math.Abs(e - d.ElevationFt) >= 1)
                {
                    diversa = true;
                    dettagli.Add($"elevazione AIP {s.Elevation}, sector {d.ElevationFt} FT");
                }
                // Lo 0 è «non scritta» (campo facoltativo): non è una differenza.
                if (transizione?.Piedi is { } t && d.TransitionAltFt > 0 && Math.Abs(t - d.TransitionAltFt) >= 1)
                {
                    diversa = true;
                    dettagli.Add($"altitudine di transizione AIP {transizione.Testo}, sector {d.TransitionAltFt} FT");
                }
                esiti.Add(new DifferenzaDiAd2(diversa ? EsitoDelConfronto.FormaDiversa : EsitoDelConfronto.Uguale, s.Icao, $"{s.Icao} {s.Name}", v.Relativo, v.Record, dettagli));
            }
        }
        return Ordinati(esiti);
    }

    private sealed record Transizione(string Testo, double? Piedi);

    /// <summary>L'altitudine di transizione di AD 2.17 per scalo: lo scalo dalla pagina (<c>AD 2 LICA 1-11</c>).</summary>
    private static Dictionary<string, Transizione> AltitudiniDiTransizione(IEnumerable<Airspace> spazi)
    {
        var per = new Dictionary<string, Transizione>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in spazi)
            if (a.Section == "AD 2.17" && a.TransitionAltitude is { } testo && ScaloDellaPagina(a.Page) is { } scalo)
                per.TryAdd(scalo, new Transizione(testo, Piedi(testo)));
        return per;
    }

    /// <summary>L'indicativo dello scalo nel riferimento di una pagina di AD 2 (<c>AD 2 LICA 1-11</c> → <c>LICA</c>), o null.</summary>
    public static string? ScaloDellaPagina(AipPage? pagina) =>
        pagina is not null && RxPaginaDiAd2().Match(pagina.Reference) is { Success: true } m ? m.Groups["scalo"].Value : null;

    [GeneratedRegex(@"^AD\s*2\s+(?<scalo>[A-Z]{4})\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxPaginaDiAd2();

    // --- le piste ----------------------------------------------------------------------------------------------------

    public static IReadOnlyList<DifferenzaDiAd2> Piste(IEnumerable<Aerodrome> scali, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(scali);
        ArgumentNullException.ThrowIfNull(sessione);
        var rw = Voci<PistaDelSector>(sessione, ".rw").ToLookup(v => v.Dato.IcaoCode.Trim(), StringComparer.OrdinalIgnoreCase);

        var esiti = new List<DifferenzaDiAd2>();
        foreach (var s in scali.OrderBy(s => s.Icao, StringComparer.OrdinalIgnoreCase))
        {
            var perDesignatore = s.Runways.GroupBy(r => Designatore(r.Designator)).ToDictionary(g => g.Key, g => g.First());
            var declinazione = Declinazione(s.MagneticVariation);
            var trovate = new HashSet<string>();
            foreach (var v in rw[s.Icao])
            {
                var d = v.Dato;
                string d1 = Designatore(d.Designator1), d2 = Designatore(d.Designator2);
                var nome = $"{s.Icao} {d.Designator1.Trim()}/{d.Designator2.Trim()}";
                var sector = $"sector ({v.File}): {d.Designator1.Trim()} {Coordinate(d.Threshold1)} {d.ElevThresh1Ft} FT {Rotta(d.TrueHeading1)}; "
                             + $"{d.Designator2.Trim()} {Coordinate(d.Threshold2)} {d.ElevThresh2Ft} FT {(d.TrueHeading2 is { } h2 ? Rotta(h2) : "—")}";
                if (!perDesignatore.TryGetValue(d1, out var p1) | !perDesignatore.TryGetValue(d2, out var p2))
                {
                    var manca = string.Join(" e ", new[] { d1, d2 }.Where(x => !perDesignatore.ContainsKey(x)));
                    esiti.Add(new DifferenzaDiAd2(EsitoDelConfronto.SoloNelSector, s.Icao, nome, v.Relativo, v.Record,
                        [sector, $"AD 2.12 non ha la {manca}"]));
                    continue;
                }
                trovate.Add(d1);
                trovate.Add(d2);
                var dettagli = new List<string> { $"AIP: {Testata(p1!)}; {Testata(p2!)}, declinazione {s.MagneticVariation ?? "—"}", sector };
                var diversa = Testata(p1!, d.Threshold1, d.ElevThresh1Ft, d.TrueHeading1, declinazione, s.MagneticVariation, dettagli);
                diversa |= Testata(p2!, d.Threshold2, d.ElevThresh2Ft, d.TrueHeading2, declinazione, s.MagneticVariation, dettagli);
                esiti.Add(new DifferenzaDiAd2(diversa ? EsitoDelConfronto.FormaDiversa : EsitoDelConfronto.Uguale, s.Icao, nome, v.Relativo, v.Record, dettagli));
            }

            // Le piste dell'AIP che nessun .rw ha: a coppie di testate opposte, dove l'AIP le ha tutte e due.
            var mancanti = s.Runways.Where(r => !trovate.Contains(Designatore(r.Designator))).ToList();
            var dette = new HashSet<string>();
            foreach (var r in mancanti)
            {
                var d = Designatore(r.Designator);
                if (!dette.Add(d)) continue;
                var opposta = mancanti.FirstOrDefault(o => Designatore(o.Designator) == Opposta(d));
                if (opposta is not null) dette.Add(Designatore(opposta.Designator));
                var nome = opposta is null ? $"{s.Icao} {r.Designator}" : $"{s.Icao} {r.Designator}/{opposta.Designator}";
                esiti.Add(new DifferenzaDiAd2(EsitoDelConfronto.MancaNelSector, s.Icao, nome, null, null,
                    [$"AIP: {Testata(r)}{(opposta is null ? "" : "; " + Testata(opposta))}"]));
            }
        }
        return Ordinati(esiti);
    }

    /// <summary>Una testata del sector contro quella dell'AIP: le righe delle differenze, e true se ce n'è una.</summary>
    private static bool Testata(PistaDellAip aip, Coordinate soglia, int elevazione, float? rotta, double? declinazione, string? stampata, List<string> dettagli)
    {
        var diversa = false;
        var nome = aip.Designator;
        if (aip.Threshold is { } t && Metri(t, soglia) is var metri && metri > SogliaDellaSogliaM)
        {
            diversa = true;
            dettagli.Add($"{nome}: soglia del sector a {metri.ToString("0", CultureInfo.InvariantCulture)} m da quella dell'AIP");
        }
        if (Piedi(aip.ThresholdElevation) is { } e && Math.Abs(e - elevazione) >= 1)
        {
            diversa = true;
            dettagli.Add($"{nome}: elevazione della soglia AIP {e.ToString("0.0", CultureInfo.InvariantCulture)} FT, sector {elevazione} FT");
        }
        if (Gradi(aip.TrueBearing) is { } vera && declinazione is { } dec && rotta is { } scritta)
        {
            var magnetica = Magnetica(vera, dec);
            var scarto = ScartoDiRotta(scritta, magnetica);
            if (scarto > SogliaDellaRotta)
            {
                diversa = true;
                dettagli.Add($"{nome}: rotta magnetica dall'AIP {vera.ToString("000.00", CultureInfo.InvariantCulture)}° vera − {stampata} = "
                             + $"{magnetica.ToString("000.0", CultureInfo.InvariantCulture)}°, sector {Rotta(scritta)} (scarto {scarto.ToString("0.0", CultureInfo.InvariantCulture)}°)");
            }
        }
        return diversa;
    }

    /// <summary>La rotta magnetica dalla vera e dalla declinazione (est positiva), fra 0 e 360.</summary>
    internal static double Magnetica(double vera, double declinazione) => ((vera - declinazione) % 360 + 360) % 360;

    /// <summary>Lo scarto fra due rotte in gradi, da 0 a 180.</summary>
    internal static double ScartoDiRotta(double a, double b) => Math.Abs(((a - b) % 360 + 540) % 360 - 180);

    /// <summary>La distanza in metri fra la soglia dell'AIP e quella del sector.</summary>
    internal static double Metri(Position aip, Coordinate sector) => Distanza(aip, sector) * MetriPerMiglio;

    private static string Testata(PistaDellAip r) =>
        $"{r.Designator} {(r.Threshold is { } t ? Coordinate(t) : "—")} {r.ThresholdElevation ?? "—"} {r.TrueBearing ?? "—"} vera";

    private static string Rotta(float rotta) => rotta.ToString("000.0", CultureInfo.InvariantCulture) + "°";

    /// <summary>Il designatore senza spazi, col numero a due cifre: <c>6</c> e <c>06</c> sono la stessa testata.</summary>
    public static string Designatore(string designatore)
    {
        var d = designatore.Trim().ToUpperInvariant();
        var cifre = new string([.. d.TakeWhile(char.IsAsciiDigit)]);
        return cifre.Length == 1 ? "0" + d : d;
    }

    /// <summary>La testata opposta: <c>16L</c> → <c>34R</c>, <c>07</c> → <c>25</c>.</summary>
    public static string Opposta(string designatore)
    {
        var d = Designatore(designatore);
        var cifre = new string([.. d.TakeWhile(char.IsAsciiDigit)]);
        if (!int.TryParse(cifre, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return d;
        var lato = d[cifre.Length..] switch { "L" => "R", "R" => "L", var x => x };
        return ((n + 17) % 36 + 1).ToString("00", CultureInfo.InvariantCulture) + lato;
    }

    /// <summary>La declinazione stampata in gradi, est positiva: <c>4° E (2020.0)</c> → 4, <c>1°30' W</c> → −1,5; o null.</summary>
    public static double? Declinazione(string? stampata)
    {
        if (stampata is null || RxDeclinazione().Match(stampata) is not { Success: true } m) return null;
        var gradi = double.Parse(m.Groups["g"].Value, CultureInfo.InvariantCulture)
                    + (m.Groups["p"].Success ? double.Parse(m.Groups["p"].Value, CultureInfo.InvariantCulture) / 60 : 0);
        return m.Groups["v"].Value == "W" ? -gradi : gradi;
    }

    [GeneratedRegex(@"(?<g>\d+(?:\.\d+)?)\s*[°º]?\s*(?:(?<p>\d+(?:\.\d+)?)\s*['′])?\s*(?<v>[EW])\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDeclinazione();

    /// <summary>I gradi di una rotta stampata: <c>098.49°</c> → 98,49; o null.</summary>
    internal static double? Gradi(string? stampata) =>
        stampata is not null && RxNumero().Match(stampata) is { Success: true } m ? double.Parse(m.Value, CultureInfo.InvariantCulture) : null;

    /// <summary>Il primo numero, in piedi: <c>19.3 FT / NIL</c> → 19,3, <c>6000 FT</c> → 6000; null se non c'è o non è in piedi.</summary>
    internal static double? Piedi(string? stampata) =>
        stampata is not null && RxPiedi().Match(stampata) is { Success: true } m ? double.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture) : null;

    [GeneratedRegex(@"\d+(?:\.\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex RxNumero();

    [GeneratedRegex(@"(?<n>\d+(?:\.\d+)?)\s*FT\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxPiedi();

    // --- le frequenze ------------------------------------------------------------------------------------------------

    public static IReadOnlyList<DifferenzaDiAd2> Frequenze(IEnumerable<Aerodrome> scali, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(scali);
        ArgumentNullException.ThrowIfNull(sessione);
        var posizioni = Voci<AtcPosition>(sessione, ".frq").Where(v => v.Dato.Code.Trim().Length > 0).ToList();

        var esiti = new List<DifferenzaDiAd2>();
        foreach (var s in scali.OrderBy(s => s.Icao, StringComparer.OrdinalIgnoreCase))
        {
            var prefisso = s.Icao + "_";
            var sue = posizioni.Where(v => v.Dato.Code.Trim().StartsWith(prefisso, StringComparison.OrdinalIgnoreCase)).ToList();
            var confrontate = s.Communications.Where(c => !ServiziFuoriDaiFrq.Contains(c.Service, StringComparer.OrdinalIgnoreCase)).ToList();
            var dellAip = new HashSet<decimal>();
            foreach (var g in confrontate.GroupBy(c => Mhz(c.Frequency)).Where(g => g.Key is not null).OrderBy(g => g.Key))
            {
                var f = g.Key!.Value;
                dellAip.Add(f);
                var testo = Frequenza(f);
                var aip = "AIP: " + string.Join(", ", g.Select(c => $"{c.Service} {c.CallSign ?? "—"}{(c.Hours is { Length: > 0 } h ? $" ({h})" : "")}"));
                var nome = $"{s.Icao} {testo} {string.Join(" / ", g.Select(c => c.CallSign ?? c.Service).Distinct())}";
                var stesse = sue.Where(v => v.Dato.FrequencyMhz == f).ToList();
                // Un avvicinamento può stare in una posizione di un altro scalo (Orio, 126.750: LIMC_ANE_APP).
                if (stesse.Count == 0 && g.Any(c => c.Service.Equals("APP", StringComparison.OrdinalIgnoreCase)))
                    stesse = [.. posizioni.Where(v => v.Dato.FrequencyMhz == f && v.Dato.Code.Trim().EndsWith("_APP", StringComparison.OrdinalIgnoreCase))];
                if (stesse.Count == 0)
                {
                    esiti.Add(new DifferenzaDiAd2(EsitoDelConfronto.MancaNelSector, s.Icao, nome, null, null, [aip, $"nessuna posizione {prefisso}… dei .frq ha {testo}"]));
                    continue;
                }
                var prima = Preferita(stesse);
                esiti.Add(new DifferenzaDiAd2(EsitoDelConfronto.Uguale, s.Icao, nome, prima.Relativo, prima.Record,
                    [aip, $"sector: {string.Join(", ", stesse.GroupBy(v => v.Dato.Code.Trim(), StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Key} ({string.Join(", ", x.Select(v => v.File))})"))}"]));
            }

            // Le posizioni dello scalo con una frequenza che AD 2.18 non ha: una riga per posizione e frequenza.
            foreach (var g in sue.Where(v => !dellAip.Contains(v.Dato.FrequencyMhz))
                         .GroupBy(v => (Codice: v.Dato.Code.Trim().ToUpperInvariant(), v.Dato.FrequencyMhz))
                         .OrderBy(g => g.Key.Codice, StringComparer.Ordinal))
            {
                var prima = Preferita([.. g]);
                esiti.Add(new DifferenzaDiAd2(EsitoDelConfronto.SoloNelSector, s.Icao, $"{g.Key.Codice} {Frequenza(g.Key.FrequencyMhz)}", prima.Relativo, prima.Record,
                    [$"sector ({string.Join(", ", g.Select(v => v.File))}): {g.Key.Codice} {Frequenza(g.Key.FrequencyMhz)}", $"AD 2.18 di {s.Icao} non ha {Frequenza(g.Key.FrequencyMhz)}"]));
            }
        }
        return Ordinati(esiti);
    }

    /// <summary>Fra le voci uguali, quella del file generale (<c>OTHER/it…</c>), poi la prima.</summary>
    private static Voce<AtcPosition> Preferita(List<Voce<AtcPosition>> voci) =>
        voci.OrderBy(v => v.File.StartsWith("it", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(v => v.Relativo, StringComparer.OrdinalIgnoreCase).First();

    /// <summary>
    /// La frequenza stampata: <c>125.900</c>, e anche il canale a 8,33 kHz <c>CH 125.630</c> (Malpensa), che il sector
    /// scrive col suo nome (<c>LIMC_ASW_APP;125.630</c>). Misura del 19 ottobre: prendere solo la prima parola le perdeva.
    /// </summary>
    private static decimal? Mhz(string stampata) =>
        RxMhz().Match(stampata) is { Success: true } m && decimal.TryParse(m.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var f) ? f : null;

    [GeneratedRegex(@"\d{3}\.\d{1,3}", RegexOptions.CultureInvariant)]
    private static partial Regex RxMhz();

    private static string Frequenza(decimal f) => f.ToString("0.000", CultureInfo.InvariantCulture);

    // --- in comune ---------------------------------------------------------------------------------------------------

    private static List<Voce<T>> Voci<T>(SessioneAperta sessione, string estensione)
    {
        var voci = new List<Voce<T>>();
        foreach (var (relativo, aperto) in sessione.File.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!relativo.EndsWith(estensione, StringComparison.OrdinalIgnoreCase) || aperto is not IFileConRecord f) continue;
            var nome = relativo[(relativo.LastIndexOfAny(['/', '\\']) + 1)..];
            for (var i = 0; i < f.RecordDelModello.Count; i++)
                if (f.RecordDelModello[i] is T dato) voci.Add(new Voce<T>(relativo, nome, i, dato));
        }
        return voci;
    }

    private static double Distanza(Position aip, Coordinate sector) => Sphere.DistanceNm(aip, new Position(sector.LatitudeDeg, sector.LongitudeDeg));

    private static string Coordinate(Position p) => string.Create(CultureInfo.InvariantCulture, $"{p.Lat:0.000000} {p.Lon:0.000000}");

    private static string Coordinate(Coordinate c) => string.Create(CultureInfo.InvariantCulture, $"{c.LatitudeDeg:0.000000} {c.LongitudeDeg:0.000000}");

    private static List<DifferenzaDiAd2> Ordinati(List<DifferenzaDiAd2> esiti) =>
    [
        .. esiti.OrderBy(e => e.Esito switch
        {
            EsitoDelConfronto.FormaDiversa => 0,
            EsitoDelConfronto.MancaNelSector => 1,
            EsitoDelConfronto.SoloNelSector => 2,
            _ => 3,
        }).ThenBy(e => e.Nome, StringComparer.OrdinalIgnoreCase),
    ];
}
