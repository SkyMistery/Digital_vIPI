using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Carta «Le curve delle procedure», §7 (committente, 1 novembre: «Sì, R6 e R7»).
/// <list type="bullet">
/// <item><b>R6</b>: la velocità delle virate che l'AIP dice nel testo della procedura o nelle sue REMARKS e non in un tratto
/// («During initial climb IAS MAX 140 KT», LIRQ RWY 05). Una sola IAS MAX, o una sola «during turn», vale per le virate senza
/// una velocità propria; due diverse senza una «during turn» no (LIPQ 185/240): si resta a 210/220 kt e si dice. Una SID che
/// non ne dice prende quella della salita iniziale della sua pista (committente: le REMARKS della salita valgono anche per le
/// SID della pista).</item>
/// <item><b>R7</b>: una salita iniziale coi rami per SID («SID AMLON 9B: turn left…; SID ANC 8B: …», LIPR RWY 12) diventa una
/// salita per ramo, col nome delle sue SID («RWY 12 per AMLON 9B»), letta con la parte comune davanti. Prima si leggeva solo
/// la parte comune, e 20 salite su 100 restavano senza tratti o col solo primo.</item>
/// </list>
/// </summary>
public static partial class ItalianAd2Prose
{
    /// <summary>R6: la velocità detta dal testo; se ne dice più d'una senza una «during turn», quali (<c>185/240</c>).</summary>
    internal readonly record struct VelocitaDelTesto(double? Nodi, string? Diverse);

    /// <summary>R6: la velocità delle virate detta da <paramref name="testi"/> (la prosa, le REMARKS).</summary>
    internal static VelocitaDelTesto VelocitaNelTesto(params string?[] testi)
    {
        var tutto = string.Join("\n", testi.Where(t => t is not null));
        var lette = RxVelocitaDelTesto().Matches(tutto)
            .Select(m => (Nodi: double.Parse(m.Groups["v"].Value, CultureInfo.InvariantCulture),
                Virata: m.Groups["prima"].Success || m.Groups["dopo"].Success || m.Groups["limitata"].Success))
            .Where(x => x.Nodi is >= 80 and <= 300)
            .ToList();
        if (lette.Where(x => x.Virata).Select(x => x.Nodi).Distinct().ToList() is [var inVirata])
            return new VelocitaDelTesto(inVirata, null);
        var diverse = lette.Select(x => x.Nodi).Distinct().Order().ToList();
        return diverse switch
        {
            [] => default,
            [var una] => new VelocitaDelTesto(una, null),
            _ => new VelocitaDelTesto(null, string.Join("/", diverse.Select(v => v.ToString("0", CultureInfo.InvariantCulture)))),
        };
    }

    /// <summary>
    /// R7: un ramo con un percorso («turn left until joining ARC 8NM RIM DME»), non solo una pendenza («304 ft/NM (5,0%) until
    /// passing FL 85», LIPX RWY 04).
    /// </summary>
    internal static bool RamoDiPercorso(string testo) => RxPercorsoNelRamo().IsMatch(testo);

    /// <summary>R7: il nome di un ramo, le SID che la condizione nomina: «SID BOA 7Z and EKTUK 7B:» → «BOA 7Z, EKTUK 7B».</summary>
    internal static string NomeDelRamo(string condizione)
    {
        var nomi = RxSidNelRamo().Matches(condizione).Select(m => $"{m.Groups["n"].Value} {m.Groups["c"].Value}")
            .Distinct(StringComparer.Ordinal).ToList();
        if (RxAltreSid().IsMatch(condizione)) nomi.Add("altre SID");
        return nomi.Count > 0 ? string.Join(", ", nomi) : RxSpazi().Replace(condizione, " ").Trim(' ', ',', ';', ':', '.');
    }

    // «IAS MAX 140 KT», «(IAS MAX 200 kt during turn)», «Turns at IAS MAX 220 kt», «Turn as IAS MAX 220 KT», «Departure turn
    // limited to 210 KT».
    [GeneratedRegex(@"(?<prima>\bTurns?\s+(?:at|as)\s+)?\bIAS\s+MAX\s*[.:]?\s*(?<v>\d{3})(?!\d)(?<dopo>[^.;)\n]{0,25}?\bduring\s+turn)?|\bturns?\s+limited\s+to\s+(?<limitata>)(?<v>\d{3})\s*KT\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxVelocitaDelTesto();

    [GeneratedRegex(@"\b(?:turn|proceed|join|intercept|follow|bound|direct|leave|track|heading|TR|RDL|ARC)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RxPercorsoNelRamo();

    [GeneratedRegex(@"\b(?<n>[A-Z]{3,5})\s?(?<c>\d{1,2}[A-Z])\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxSidNelRamo();

    // R7, LIEE RWY 32 (4): nel PDF il testo italiano si mescola all'inglese — «on su track 300°», «on track 105°. 300° until».
    [GeneratedRegex(@"\bon\s+su\s+track\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxSuDavantiATrack();

    [GeneratedRegex(@"\btrack\s+(\d{3})°\.\s+(\d{3})°(?=\s+(?:until|till)\b)", RegexOptions.CultureInvariant)]
    private static partial Regex RxDueRotteDiFila();
}
