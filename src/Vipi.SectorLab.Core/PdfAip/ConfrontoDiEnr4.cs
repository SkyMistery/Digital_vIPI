using System.Globalization;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Una riga del confronto di ENR 4 (carta F6, slice 3b): una radioassistenza o un punto, com'è rispetto al sector, con le
/// righe che dicono le differenze. <paramref name="File"/>/<paramref name="Record"/>: la voce del sector da aprire (la chiave
/// del file nella sessione, <c>Include/IT/NAVAIDS/itfix.fix</c>).
/// </summary>
public sealed record DifferenzaDiEnr4(EsitoDelConfronto Esito, string Nome, string? File, int? Record, IReadOnlyList<string> Dettagli)
{
    /// <summary>
    /// Per un punto uguale di posizione: l'uso (il 4° campo del <c>.fix</c>) o il FRA (il 5°) non sono quelli che l'AIP
    /// fa pensare. Non cambia l'esito: è una proposta da misurare, non una differenza (carta F6 §6, slice 3b).
    /// </summary>
    public bool UsoDiverso { get; init; }
}

/// <summary>
/// Il confronto di ENR 4 col sector, in sola lettura (slice 3b).
/// <para><b>Le radioassistenze</b> (ENR 4.1) coi <c>.vor</c> (VOR, DVOR, VOR/DME, VORTAC, TACAN: tutto quel che ha una
/// frequenza VHF o un canale) e coi <c>.ndb</c> (NDB e locator): per sigla, fra le voci del sector con quella sigla la più
/// vicina; la posizione oltre 0,1 NM (D4), la frequenza, il canale TACAN dove il sector lo scrive. Una voce del sector che
/// ENR 4.1 non ha è di solito di uno scalo (AD 2) o di fuori: si dice.</para>
/// <para><b>I punti</b> (ENR 4.4) coi <c>.fix</c> di tutti i file: per nome, il più vicino; oltre 0,1 NM è «stesso nome,
/// altro punto» (TIMOV e XOPTA, carta madre §11). Un punto di <c>itfix.fix</c> che ENR 4.4 non ha si dice; quelli degli
/// altri <c>.fix</c> (di scalo, esterni, militari) no: non sono di ENR 4.4.</para>
/// </summary>
public static class ConfrontoDiEnr4
{
    /// <summary>Il file del sector che ha i punti di ENR 4.4 (gli altri <c>.fix</c> sono di scalo, esterni, militari).</summary>
    public const string FileDeiPunti = "itfix.fix";

    /// <summary>Una voce del sector: la chiave del file nella sessione (per aprirla), il suo nome (per dirlo), il record.</summary>
    private sealed record Voce<T>(string Relativo, string File, int Record, T Dato);

    // --- le radioassistenze ------------------------------------------------------------------------------------------

    public static IReadOnlyList<DifferenzaDiEnr4> Radioassistenze(IEnumerable<Navaid> radioassistenze, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(radioassistenze);
        ArgumentNullException.ThrowIfNull(sessione);
        var vor = Voci<Vor>(sessione, ".vor").GroupBy(v => v.Dato.Ident.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var ndb = Voci<Ndb>(sessione, ".ndb").GroupBy(v => v.Dato.Ident.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var usateVor = new HashSet<(string, int)>();
        var usateNdb = new HashSet<(string, int)>();

        var esiti = new List<DifferenzaDiEnr4>();
        foreach (var n in radioassistenze.OrderBy(n => n.Designator, StringComparer.OrdinalIgnoreCase).ThenBy(n => n.Type, StringComparer.Ordinal))
        {
            var nome = n.Key;
            var aip = $"AIP: {n.Name}, {n.Frequency ?? "—"}{(n.Channel is { } c ? $" CH {c}" : "")}, {Coordinate(n.Position)}{(n.Military ? " (militare, ENR 4.1.2)" : "")}";
            if (Ndb(n))
            {
                if (ndb.GetValueOrDefault(n.Designator) is not { } suoi)
                {
                    esiti.Add(new DifferenzaDiEnr4(EsitoDelConfronto.MancaNelSector, nome, null, null, [aip]));
                    continue;
                }
                var suo = suoi.MinBy(v => Distanza(n.Position, v.Dato.Position))!;
                usateNdb.Add((suo.Relativo, suo.Record));
                var dettagli = new List<string> { aip, $"sector ({suo.File}): {suo.Dato.Frequency.ToString(CultureInfo.InvariantCulture)} kHz, {Coordinate(suo.Dato.Position)}" };
                var diversa = Posizione(n.Position, suo.Dato.Position, dettagli);
                if (Frequenza(n.Frequency) is { } f && f != suo.Dato.Frequency)
                {
                    diversa = true;
                    dettagli.Add($"frequenza AIP {f.ToString(CultureInfo.InvariantCulture)} kHz, sector {suo.Dato.Frequency.ToString(CultureInfo.InvariantCulture)}");
                }
                esiti.Add(new DifferenzaDiEnr4(diversa ? EsitoDelConfronto.FormaDiversa : EsitoDelConfronto.Uguale, nome, suo.Relativo, suo.Record, dettagli));
            }
            else
            {
                if (vor.GetValueOrDefault(n.Designator) is not { } suoi)
                {
                    esiti.Add(new DifferenzaDiEnr4(EsitoDelConfronto.MancaNelSector, nome, null, null, [aip]));
                    continue;
                }
                var suo = suoi.MinBy(v => Distanza(n.Position, v.Dato.Position))!;
                usateVor.Add((suo.Relativo, suo.Record));
                var v = suo.Dato;
                var dettagli = new List<string>
                {
                    aip,
                    $"sector ({suo.File}): {(v.Frequency is { } fs ? fs.ToString(CultureInfo.InvariantCulture) : "—")}{(v.CanaleTacan is { Length: > 0 } cs ? $" CH {cs}" : "")}, {Coordinate(v.Position)}",
                };
                var diversa = Posizione(n.Position, v.Position, dettagli);
                if (Frequenza(n.Frequency) is { } f && f != v.Frequency)
                {
                    diversa = true;
                    dettagli.Add($"frequenza AIP {f.ToString(CultureInfo.InvariantCulture)} MHz, sector {(v.Frequency is { } x ? x.ToString(CultureInfo.InvariantCulture) : "—")}");
                }
                // Il canale si confronta dove il sector lo scrive: senza, non è una differenza (il campo è facoltativo).
                if (n.Channel is { } canale && v.CanaleTacan is { Length: > 0 } scritto && !string.Equals(canale, scritto.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    diversa = true;
                    dettagli.Add($"canale AIP {canale}, sector {scritto.Trim()}");
                }
                esiti.Add(new DifferenzaDiEnr4(diversa ? EsitoDelConfronto.FormaDiversa : EsitoDelConfronto.Uguale, nome, suo.Relativo, suo.Record, dettagli));
            }
        }

        foreach (var v in vor.Values.SelectMany(x => x).Where(v => !usateVor.Contains((v.Relativo, v.Record))).OrderBy(v => v.Dato.Ident, StringComparer.OrdinalIgnoreCase))
            esiti.Add(new DifferenzaDiEnr4(EsitoDelConfronto.SoloNelSector, v.Dato.Ident, v.Relativo, v.Record,
                [$"sector ({v.File}): {(v.Dato.Frequency is { } f ? f.ToString(CultureInfo.InvariantCulture) : "—")}, {Coordinate(v.Dato.Position)}", "non in ENR 4.1: di uno scalo (AD 2) o di fuori, che il Lab non legge ancora"]));
        foreach (var v in ndb.Values.SelectMany(x => x).Where(v => !usateNdb.Contains((v.Relativo, v.Record))).OrderBy(v => v.Dato.Ident, StringComparer.OrdinalIgnoreCase))
            esiti.Add(new DifferenzaDiEnr4(EsitoDelConfronto.SoloNelSector, v.Dato.Ident, v.Relativo, v.Record,
                [$"sector ({v.File}): {v.Dato.Frequency.ToString(CultureInfo.InvariantCulture)} kHz, {Coordinate(v.Dato.Position)}", "non in ENR 4.1: di uno scalo (AD 2) o di fuori, che il Lab non legge ancora"]));

        return Ordinati(esiti);
    }

    /// <summary>NDB e locator vanno nei <c>.ndb</c>; tutto il resto (VOR, DME, TACAN) nei <c>.vor</c>.</summary>
    private static bool Ndb(Navaid n) => n.Kind is NavaidKind.Ndb or NavaidKind.Locator;

    /// <summary>Il numero della frequenza stampata (<c>113.80 MHZ</c> → 113.80), o null.</summary>
    private static decimal? Frequenza(string? testo) =>
        testo is not null && decimal.TryParse(testo.Split(' ')[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var f) ? f : null;

    // --- i punti -----------------------------------------------------------------------------------------------------

    public static IReadOnlyList<DifferenzaDiEnr4> Punti(IEnumerable<SignificantPoint> punti, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(punti);
        ArgumentNullException.ThrowIfNull(sessione);
        var fix = Voci<Fix>(sessione, ".fix").GroupBy(v => v.Dato.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var esiti = new List<DifferenzaDiEnr4>();
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in punti.OrderBy(p => p.Designator, StringComparer.OrdinalIgnoreCase))
        {
            visti.Add(p.Designator);
            var aip = $"AIP: {Coordinate(p.Position)}, rotte {(p.Routes.Count > 0 ? string.Join(" ", p.Routes) : "—")}, procedure {p.Procedures ?? "—"}, FRA {p.Fra ?? "—"}";
            if (fix.GetValueOrDefault(p.Designator) is not { } suoi)
            {
                esiti.Add(new DifferenzaDiEnr4(EsitoDelConfronto.MancaNelSector, p.Designator, null, null, [aip]));
                continue;
            }
            // Il più vicino; a parità, quello di itfix.fix (il file di ENR 4.4).
            var suo = suoi.OrderBy(v => Distanza(p.Position, v.Dato.Position)).ThenBy(v => DelFileDeiPunti(v.File) ? 0 : 1).First();
            var dettagli = new List<string> { aip, $"sector ({suo.File}): {Coordinate(suo.Dato.Position)}, uso {suo.Dato.DisplayType?.ToString(CultureInfo.InvariantCulture) ?? "—"}, FRA {suo.Dato.ExtraField ?? "—"}" };
            var diversa = Posizione(p.Position, suo.Dato.Position, dettagli, "stesso nome, altro punto: ");
            foreach (var altro in suoi.Where(v => v != suo && Distanza(p.Position, v.Dato.Position) > ConfrontoDiEnr3.Soglia))
                dettagli.Add($"anche in {altro.File}, a {Distanza(p.Position, altro.Dato.Position).ToString("0.00", CultureInfo.InvariantCulture)} NM");

            var usoDiverso = false;
            if (DelFileDeiPunti(suo.File))
            {
                if (UsoAtteso(p) is { } uso && suo.Dato.DisplayType is { } scritto && scritto != uso)
                {
                    usoDiverso = true;
                    dettagli.Add($"uso: dall'AIP {uso} ({Uso(uso)}), sector {scritto} ({Uso(scritto)})");
                }
                var fra = p.Fra is null ? "0" : "1";
                if (suo.Dato.ExtraField is { } f && f.Trim() != fra)
                {
                    usoDiverso = true;
                    dettagli.Add($"FRA: nell'AIP {(p.Fra is null ? "no" : "sì")}, sector {f.Trim()}");
                }
            }
            esiti.Add(new DifferenzaDiEnr4(diversa ? EsitoDelConfronto.FormaDiversa : EsitoDelConfronto.Uguale, p.Designator, suo.Relativo, suo.Record, dettagli)
            {
                UsoDiverso = !diversa && usoDiverso,
            });
        }

        foreach (var v in fix.Values.SelectMany(x => x).Where(v => DelFileDeiPunti(v.File) && !visti.Contains(v.Dato.Name.Trim()))
                     .OrderBy(v => v.Dato.Name, StringComparer.OrdinalIgnoreCase))
            esiti.Add(new DifferenzaDiEnr4(EsitoDelConfronto.SoloNelSector, v.Dato.Name, v.Relativo, v.Record, [$"sector ({v.File}): {Coordinate(v.Dato.Position)}"]));

        return Ordinati(esiti);
    }

    /// <summary>
    /// L'uso che l'AIP fa pensare, nella numerazione del 4° campo del <c>.fix</c> (manuale IVAO: 0 in rotta, 1 terminale, 2
    /// tutti e due): rotte ATS → in rotta, procedure → terminale. Null se l'AIP non dà né rotte né procedure.
    /// </summary>
    private static int? UsoAtteso(SignificantPoint p) => (p.Routes.Count > 0, p.Procedures is not null) switch
    {
        (true, true) => 2,
        (true, false) => 0,
        (false, true) => 1,
        _ => null,
    };

    private static string Uso(int uso) => uso switch { 0 => "in rotta", 1 => "terminale", 2 => "tutti e due", 3 => "nascosto", _ => "?" };

    private static bool DelFileDeiPunti(string file) => string.Equals(file, FileDeiPunti, StringComparison.OrdinalIgnoreCase);

    // --- in comune ---------------------------------------------------------------------------------------------------

    /// <summary>Le voci di tipo <typeparamref name="T"/> dei file con quell'estensione, col nome del file e il record.</summary>
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

    /// <summary>La posizione oltre la soglia (D4): la riga che lo dice, e true.</summary>
    private static bool Posizione(Position aip, Coordinate sector, List<string> dettagli, string prima = "")
    {
        var scarto = Distanza(aip, sector);
        if (scarto <= ConfrontoDiEnr3.Soglia) return false;
        dettagli.Add($"{prima}nel sector a {scarto.ToString("0.00", CultureInfo.InvariantCulture)} NM da dove lo mette l'AIP");
        return true;
    }

    private static string Coordinate(Position p) => string.Create(CultureInfo.InvariantCulture, $"{p.Lat:0.00000} {p.Lon:0.00000}");

    private static string Coordinate(Coordinate c) => string.Create(CultureInfo.InvariantCulture, $"{c.LatitudeDeg:0.00000} {c.LongitudeDeg:0.00000}");

    private static List<DifferenzaDiEnr4> Ordinati(List<DifferenzaDiEnr4> esiti) =>
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
