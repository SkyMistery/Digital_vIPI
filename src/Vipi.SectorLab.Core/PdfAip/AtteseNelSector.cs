using System.Globalization;
using System.Text.RegularExpressions;
using Aip;
using Aip.Aurora;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>Uno scalo le cui procedure passano per il punto dell'attesa: lo <c>.str</c> e le piste della procedura.</summary>
public sealed record ScaloDellAttesa(string Icao, string File, string Piste);

/// <summary>
/// Dove e come portare un'attesa dell'AIP nel sector (slice 2c, committente 19 ottobre): «di procedura» se il punto è nelle
/// procedure di uno scalo (<see cref="Scali"/>, il primo è la proposta), altrimenti «in rotta»; la lunghezza del tratto per
/// l'ovale, col perché. <paramref name="Punto"/>: dove il sector ha il punto (null: non si porta).
/// </summary>
public sealed record PropostaDiAttesa(Holding Attesa, IReadOnlyList<ScaloDellAttesa> Scali, Position? Punto, double TrattoNm, string Tratto);

/// <summary>
/// Le attese dell'AIP portate nel sector (slice 2c). Due casi (committente, 19 ottobre):
/// <list type="bullet">
/// <item><b>di procedura</b>: il punto compare nelle procedure (SID, STAR, avvicinamenti) di uno scalo → una voce
/// <c>ICAO;PISTE;HLD-PUNTO; ; ;2;</c> nello <c>.str</c> dello scalo, come quelle di oggi (<c>liba.str</c>, <c>HLD-ELVAD</c>):
/// il commento <c>//PUNTO</c>, la riga <c>PUNTO;PUNTO;</c> (la scritta) e, se si sceglie l'ovale, i suoi punti;</item>
/// <item><b>in rotta</b>: una riga di <c>HOLDENR.hold</c>, <c>HLD-PUNTO;lat;lon;PUNTO/RRRV-QUOTA;</c> (la scritta). L'ovale
/// in rotta: dove scriverlo lo decide il committente più avanti.</item>
/// </list>
/// L'ovale: il punto alla fine del tratto d'avvicinamento, virate di 180° a 3°/s, il tratto lungo quanto dice l'AIP (in NM;
/// o in minuti alla IAS massima delle note; senza note 1 minuto sotto FL140 e 1,5 sopra, a 230 kt — scelta del
/// committente). ⚠️ La rotta d'avvicinamento dell'AIP è magnetica: l'ovale la usa come vera (in Italia la declinazione è di
/// 2-4° est), e la scheda lo dice.
/// </summary>
public static partial class AtteseNelSector
{
    public const double IasDiBase = 230;

    [GeneratedRegex(@"(?:Outbound\s+Leg|Leg\s+Distance)\s*(?<nm>\d+(?:[.,]\d+)?)\s*NM", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxTrattoInNm();

    [GeneratedRegex(@"Timing\s*-?\s*(?<min>\d+(?:[.,]\d+)?)\s*min", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxMinuti();

    [GeneratedRegex(@"IAS\s*MAX\s*(?<kt>\d+)\s*kt", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture)]
    private static partial Regex RxIas();

    /// <summary>Le proposte per le attese dell'AIP: gli scali delle procedure che passano per il punto, il punto, il tratto.</summary>
    public static IReadOnlyDictionary<string, PropostaDiAttesa> Proposte(IEnumerable<Holding> attese, SessioneAperta sessione, Func<string, Coordinate?> puntoDelSector)
    {
        ArgumentNullException.ThrowIfNull(attese);
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(puntoDelSector);
        // I punti delle procedure di ogni scalo (gli .str): punto → scali, nell'ordine dei file.
        var scali = new Dictionary<string, List<ScaloDellAttesa>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relativo, aperto) in sessione.File.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (!relativo.EndsWith(".str", StringComparison.OrdinalIgnoreCase) || aperto is not IFileConRecord str) continue;
            foreach (var p in str.RecordDelModello.OfType<ProcedureStrRecord>())
            {
                if (p.RunwaySpec.Equals("MAPS", StringComparison.OrdinalIgnoreCase) || p.ProcedureId.StartsWith("HLD-", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var w in p.Waypoints)
                {
                    if (!scali.TryGetValue(w.FixName, out var suoi)) scali[w.FixName] = suoi = [];
                    if (!suoi.Any(s => s.Icao == p.IcaoCode)) suoi.Add(new ScaloDellAttesa(p.IcaoCode, relativo, p.RunwaySpec));
                }
            }
        }

        var proposte = new Dictionary<string, PropostaDiAttesa>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in attese)
        {
            if (proposte.ContainsKey(a.Fix)) continue;
            var (nm, perche) = Tratto(a);
            proposte[a.Fix] = new PropostaDiAttesa(a, scali.GetValueOrDefault(a.Fix) ?? [],
                puntoDelSector(a.Fix) is { } c ? new Position(c.LatitudeDeg, c.LongitudeDeg) : null, nm, perche);
        }
        return proposte;
    }

    /// <summary>La lunghezza del tratto in NM, e da dove viene (le note dell'AIP, o la scelta di base).</summary>
    public static (double Nm, string Perche) Tratto(Holding a)
    {
        ArgumentNullException.ThrowIfNull(a);
        var note = a.Remarks ?? "";
        if (RxTrattoInNm().Match(note) is { Success: true } m)
            return (Numero(m.Groups["nm"].Value), $"{m.Groups["nm"].Value} NM dalle note dell'AIP");
        var ias = RxIas().Match(note) is { Success: true } k ? Numero(k.Groups["kt"].Value) : IasDiBase;
        var alto = a.Minimum.Reference == VerticalReference.FlightLevel ? (a.Minimum.Value ?? 0) * 100 : a.Minimum.Value ?? 0;
        var minuti = RxMinuti().Match(note) is { Success: true } t ? Numero(t.Groups["min"].Value) : alto > 14000 ? 1.5 : 1.0;
        var nm = Math.Round(minuti * ias / 60, 1);
        return (nm, $"{minuti.ToString("0.#", CultureInfo.InvariantCulture)} min a {ias.ToString("0", CultureInfo.InvariantCulture)} kt"
                    + (RxIas().IsMatch(note) ? "" : " (di base)"));
    }

    private static double Numero(string s) => double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

    /// <summary>
    /// L'ovale: dal punto (fine del tratto d'avvicinamento) la virata di 180° verso il lato dell'attesa, il tratto
    /// d'allontanamento, l'altra virata, il tratto d'avvicinamento fino al punto. Chiuso: l'ultimo punto ripete il primo.
    /// </summary>
    public static IReadOnlyList<Position> Ovale(Position punto, int avvicinamento, TurnDirection virata, double trattoNm, double iasKt = IasDiBase)
    {
        // Il raggio di una virata a 3°/s: il giro intero in 2 minuti.
        var raggio = iasKt / 60 * 2 / (2 * Math.PI);
        var lato = virata == TurnDirection.Right ? 90.0 : -90.0;
        var verso = virata == TurnDirection.Right ? 1.0 : -1.0;
        var centroVicino = Sphere.Destination(punto, avvicinamento + lato, raggio);
        var inizio = Sphere.Destination(punto, avvicinamento + 180, trattoNm);
        var centroLontano = Sphere.Destination(inizio, avvicinamento + lato, raggio);
        var punti = new List<Position>();
        // Prima virata, attorno al centro vicino: dal punto al lato opposto, 180° nel verso della virata, ogni 10°.
        for (var g = 0; g <= 180; g += 10)
            punti.Add(Sphere.Destination(centroVicino, avvicinamento - lato + verso * g, raggio));
        // Seconda virata, attorno al centro lontano: dall'abeam dell'inizio all'inizio del tratto d'avvicinamento.
        for (var g = 0; g <= 180; g += 10)
            punti.Add(Sphere.Destination(centroLontano, avvicinamento + lato + verso * g, raggio));
        // Il primo punto è il punto dell'attesa, esatto (dal centro tornerebbe a qualche centesimo di secondo).
        punti[0] = punto;
        punti.Add(punto);
        return punti;
    }

    /// <summary>La quota come la scrive <c>HOLDENR.hold</c> (<c>9000</c>, <c>FL110</c>), o null se non ha quella forma («TRL»).</summary>
    public static string? Quota(Holding a)
    {
        var testo = a.Minimum.Text.Split(' ')[0].ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);
        return Regex.IsMatch(testo, @"^(?:FL\d{2,3}|\d{3,5})$", RegexOptions.CultureInvariant) ? testo : null;
    }

    /// <summary>La descrizione di <c>HOLDENR.hold</c>: <c>ABBOZ/225R-9000</c>.</summary>
    public static string? Descrizione(Holding a) =>
        Quota(a) is { } q ? $"{a.Fix}/{a.InboundTrack:000}{(a.Turn == TurnDirection.Right ? "R" : "L")}-{q}" : null;

    /// <summary>La riga di <c>HOLDENR.hold</c> per un'attesa in rotta, o null se la quota non ha la forma del file.</summary>
    public static string? RigaInRotta(Holding a, Position punto) =>
        Descrizione(a) is { } d ? $"HLD-{a.Fix};{AuroraText.Coordinates(punto)};{d};" : null;

    /// <summary>Le righe della voce di un'attesa di procedura nello <c>.str</c> dello scalo: testata, commento, scritta e, se c'è, l'ovale.</summary>
    public static IReadOnlyList<string> RigheDiProcedura(Holding a, ScaloDellAttesa scalo, IReadOnlyList<Position>? ovale)
    {
        var righe = new List<string> { $"{scalo.Icao};{scalo.Piste};HLD-{a.Fix}; ; ;2;", $"//{a.Fix}", $"{a.Fix};{a.Fix};" };
        if (ovale is not null)
            righe.AddRange(ovale.Select(p => AuroraText.Coordinates(p) + ";"));
        return righe;
    }

    /// <summary>La IAS massima delle note dell'AIP (per il raggio delle virate), o quella di base.</summary>
    public static double Ias(Holding a) =>
        RxIas().Match(a.Remarks ?? "") is { Success: true } k ? Numero(k.Groups["kt"].Value) : IasDiBase;
}
