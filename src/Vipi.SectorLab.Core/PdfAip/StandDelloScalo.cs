using System.Globalization;
using System.Text.RegularExpressions;
using Aip;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Un punto stampato sulla carta dei parcheggi: lo stand (<c>110</c>), il nome com'è stampato (<c>110-stop2</c>), dove sta e
/// la pagina.
/// </summary>
public sealed record PuntoDelloStand(string Stand, string Nome, Position Posizione, string Pagina);

/// <summary>
/// Uno stand del sector (una riga dei <c>.gts</c>) contro i punti dell'AIP con lo stesso nome: il punto più vicino e a quanti
/// metri. <see cref="Punto"/> null: l'AIP non stampa quello stand.
/// </summary>
public sealed record StandNelGround(string Stand, string File, int Record, PuntoDelloStand? Punto, double? Metri)
{
    /// <summary>Oltre <see cref="StandDelloScalo.SogliaDelloStandM"/>.</summary>
    public bool Diverso => Metri > StandDelloScalo.SogliaDelloStandM;
}

/// <summary>Gli stand del sector contro i punti dell'AIP, e gli stand dell'AIP che nessun <c>.gts</c> ha.</summary>
public sealed record ConfrontoDegliStand(IReadOnlyList<StandNelGround> Stand, IReadOnlyList<string> MancanoNelSector, IReadOnlyList<string> File);

/// <summary>
/// Gli stand del sector contro l'AIP, in metri (carta «Gli aeroporti nel Lab», §2 D, controllo 3): dove la carta dei
/// parcheggi stampa le coordinate degli stand, ogni riga dei <c>.gts</c> contro il punto dell'AIP dello stesso stand.
/// <para>Sul 2611 le stampano LIPE (i punti d'arresto, «110-stop1», «110-stop2»), LIEE, LIPY e LIRP, scritte sul disegno: le
/// righe di <see cref="NoteDeiParcheggi"/> le portano, un nome seguito da latitudine e longitudine, a volte con « - » fra le
/// due, e più punti sulla stessa riga. Una coordinata senza un nome davanti si salta: sul disegno il nome può stare su
/// un'altra riga, e indovinarlo sarebbe sbagliare lo stand.</para>
/// </summary>
public static partial class StandDelloScalo
{
    /// <summary>
    /// Uno stand oltre questi metri dal punto dell'AIP è diverso: la stessa soglia delle soglie delle piste. Sul 2611 gli stand
    /// del sector di LIPE che tornano stanno sotto il metro (copiati dall'AIP al centesimo di secondo).
    /// </summary>
    public const double SogliaDelloStandM = 10;

    private const string Gms = @"\d{2,3}\s?[°º]\s?\d{2}\s?['’]\s?\d{1,2}(?:[.,]\d+)?\s?(?:''|""|”|″)?(?:,\d+)?";

    [GeneratedRegex(@"(?<![\w.,/-])(?<nome>[A-Z]?\d{1,4}[A-Z]?(?:-stop\s?\d)?)\s+(?<lat>" + Gms + @")\s?(?:N\s?)?(?:-\s?)?(?<lon>" + Gms + ")",
        RegexOptions.CultureInvariant)]
    private static partial Regex RxPunto();

    [GeneratedRegex(@"(?<g>\d{2,3})\s?[°º]\s?(?<p>\d{2})\s?['’]\s?(?<s>\d{1,2})(?:[.,](?<d>\d+))?\s?(?:''|""|”|″)?(?:,(?<d2>\d+))?", RegexOptions.CultureInvariant)]
    private static partial Regex RxGms();

    [GeneratedRegex(@"-stop\s?\d$", RegexOptions.CultureInvariant)]
    private static partial Regex RxFermata();

    /// <summary>Un punto d'arresto nel sector: lo stand seguito da «S» e un numero (<c>29S1</c>).</summary>
    [GeneratedRegex(@"^(?<stand>\d{1,4})S\d$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxFermataDelSector();

    /// <summary>I punti degli stand stampati nelle pagine di note, in ordine di pagina e di riga.</summary>
    public static IReadOnlyList<PuntoDelloStand> Punti(IEnumerable<PaginaDiNote> note)
    {
        ArgumentNullException.ThrowIfNull(note);
        var punti = new List<PuntoDelloStand>();
        foreach (var pagina in note)
            foreach (var riga in pagina.Righe)
                foreach (Match m in RxPunto().Matches(riga))
                {
                    if (Gradi(m.Groups["lat"].Value) is not { } lat || Gradi(m.Groups["lon"].Value) is not { } lon || lat is < 35 or > 48 || lon is < 6 or > 19)
                        continue;
                    var nome = Regex.Replace(m.Groups["nome"].Value, @"\s+", "");
                    punti.Add(new PuntoDelloStand(RxFermata().Replace(nome, ""), nome, new Position(lat, lon), pagina.Pagina));
                }
        return punti;
    }

    /// <summary>Gradi, primi e secondi stampati in gradi decimali; null se i primi o i secondi non tornano.</summary>
    public static double? Gradi(string testo)
    {
        if (RxGms().Match(testo ?? "") is not { Success: true } m)
            return null;
        var g = int.Parse(m.Groups["g"].Value, CultureInfo.InvariantCulture);
        var p = int.Parse(m.Groups["p"].Value, CultureInfo.InvariantCulture);
        var decimali = m.Groups["d"].Success ? m.Groups["d"].Value : m.Groups["d2"].Value;
        var s = double.Parse(m.Groups["s"].Value + (decimali.Length > 0 ? "." + decimali : ""), CultureInfo.InvariantCulture);
        return p >= 60 || s >= 60 ? null : g + p / 60.0 + s / 3600.0;
    }

    /// <summary>
    /// Gli stand dei <c>.gts</c> dello scalo contro i punti dell'AIP: per ogni riga il punto dello stesso stand più vicino (uno
    /// stand può avere più punti d'arresto). Il nome si confronta senza zeri davanti e senza maiuscole («07» è «7»).
    /// </summary>
    public static ConfrontoDegliStand Confronta(IReadOnlyList<PuntoDelloStand> punti, string icao, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(punti);
        ArgumentNullException.ThrowIfNull(sessione);
        var perStand = punti.ToLookup(p => Chiave(p.Stand), StringComparer.Ordinal);
        var gts = ConfrontoDiAd2.Voci<Stand>(sessione, ".gts")
            .Where(v => v.Dato.IcaoCode.Trim().Equals(icao, StringComparison.OrdinalIgnoreCase) && v.Dato.Number.Trim().Length > 0).ToList();
        var righe = new List<StandNelGround>();
        foreach (var v in gts)
        {
            // LIRP: il sector scrive i punti d'arresto «29S1», «29S2», l'AIP lo stand «29».
            var candidati = perStand[Chiave(v.Dato.Number)].ToList();
            if (candidati.Count == 0 && RxFermataDelSector().Match(v.Dato.Number.Trim()) is { Success: true } f)
                candidati = [.. perStand[Chiave(f.Groups["stand"].Value)]];
            var piuVicino = candidati.Select(p => (Punto: p, Metri: ConfrontoDiAd2.Metri(p.Posizione, v.Dato.Position)))
                .OrderBy(x => x.Metri).Select(x => ((PuntoDelloStand, double)?)x).FirstOrDefault();
            righe.Add(new StandNelGround(v.Dato.Number.Trim(), v.Relativo, v.Record, piuVicino?.Item1, piuVicino?.Item2));
        }
        var nelSector = gts.Select(v => Chiave(RxFermataDelSector().Match(v.Dato.Number.Trim()) is { Success: true } f ? f.Groups["stand"].Value : v.Dato.Number))
            .Concat(gts.Select(v => Chiave(v.Dato.Number))).ToHashSet(StringComparer.Ordinal);
        var mancano = punti.Select(p => p.Stand).Distinct(StringComparer.Ordinal).Where(s => !nelSector.Contains(Chiave(s))).ToList();
        return new ConfrontoDegliStand(righe, mancano, [.. gts.Select(v => v.Relativo).Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    private static string Chiave(string stand)
    {
        var s = stand.Trim().ToUpperInvariant().TrimStart('0');
        return s.Length == 0 ? "0" : s;
    }
}
