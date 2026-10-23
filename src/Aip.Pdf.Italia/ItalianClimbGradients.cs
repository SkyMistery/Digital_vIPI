using System.Globalization;
using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Le pendenze minime di salita stampate (segnalazioni 2611, passo 5 della revisione col committente): «Minimum climb gradient
/// 8.3% (500 ft/NM) until leaving 4500FT», «- 365 ft/NM - 6.0% until passing FL90 for ATC reasons (a gradient of 300 ft/NM -
/// 4.9% until passing 6000ft provides separation from obstacles)», «350 ft/NM (5.76%) until passing 4000 ft due to obstacles;
/// 500 ft/NM (8.2%) until passing 4000 ft for ATC reasons». Ogni pendenza col valore (ft/NM, %, o tutti e due), fino a che
/// quota, il perché (ostacoli, ATC) e le SID a cui vale, se il testo le nomina («For the following SIDs: OSKOR 7W and UVNIB
/// 1W», «if cleared via SID KOVAS 6D», «for LAKOX 7W, GITOD 8W»); senza nomi vale per tutte quelle del testo.
/// </summary>
public static partial class ItalianClimbGradients
{
    /// <summary>Una pendenza letta, con le SID che nomina (vuoto: tutte).</summary>
    public sealed record Letta(ClimbGradient Pendenza, IReadOnlyList<string> Sid);

    public static IReadOnlyList<Letta> Read(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return [];
        var t = RxSpazi().Replace(testo, " ");
        var lette = new List<Letta>();
        foreach (Match contesto in RxContesto().Matches(t))
        {
            // Il contesto: dalla parola «gradient» fino al prossimo «gradient», o alla fine.
            var inizio = contesto.Index;
            var fine = RxContesto().Match(t, contesto.Index + contesto.Length) is { Success: true } dopo ? dopo.Index : t.Length;
            var pezzo = t[inizio..fine];
            // Le avvertenze dopo non sono pendenze («Close-in obstacles penetrating OIS 2.5% exist…»).
            if (RxFineContesto().Match(pezzo, contesto.Length) is { Success: true } avvertenza) pezzo = pezzo[..avvertenza.Index];
            // «For LAKOX 7W, GITOD 8W, KATAR 9W, Minimum climb gradient: …» (LIRZ): le SID davanti.
            List<string> davanti = RxPerDavanti().Match(t[Math.Max(0, inizio - 120)..inizio]) is { Success: true } pd ? Sid(pd.Value) : [];
            // «For the following SIDs: X and Y» vale per i valori che seguono, fino al prossimo «For the following».
            var testa = RxPerLeSeguenti().Matches(pezzo).Select(m => m.Index).Prepend(0).Distinct().Order().ToList();
            for (var i = 0; i < testa.Count; i++)
            {
                var parte = pezzo[testa[i]..(i + 1 < testa.Count ? testa[i + 1] : pezzo.Length)];
                var valori = RxValore().Matches(parte).ToList();
                if (valori.Count == 0) continue;
                var sidDellaParte = RxPerLeSeguenti().IsMatch(parte) ? Sid(parte[..valori[0].Index]) : davanti;
                // Il perché detto in testa vale per tutti («Minimum climb gradient (ATC): - 300 ft/NM …», LIEE).
                var perDefault = Perche(parte[..valori[0].Index]);
                for (var j = 0; j < valori.Count; j++)
                {
                    var v = valori[j];
                    var coda = parte[(v.Index + v.Length)..(j + 1 < valori.Count ? valori[j + 1].Index : parte.Length)];
                    // La coda finisce al punto e virgola, alla fine della frase, a «then: a) …»: dopo è un'altra voce.
                    if (RxFineVoce().Match(coda) is { Success: true } f) coda = coda[..f.Index];
                    // «8.5% (515ft/NM) till passing 2000ft (for SID OBAXU 5B until FL 100)» (LICB RWY 05, passo 8): la pendenza
                    // vale per tutte, e per le SID fra parentesi fino a un'altra quota.
                    var perLoro = RxPerSidFraParentesi().Match(coda);
                    if (perLoro.Success) coda = coda.Remove(perLoro.Index, perLoro.Length);
                    // Le SID dette davanti al valore, dall'ultimo punto e virgola («a) if proceeding via SID ECVAZ 8A, OTZOM 7A: 395
                    // ft/NM», LIPX), se no quelle in coda («300 ft/NM (4.9%) up to FL 150 if cleared via SID KOVAS 6E»).
                    var davantiAlValore = parte[(j == 0 ? 0 : valori[j - 1].Index + valori[j - 1].Length)..v.Index];
                    if (davantiAlValore.LastIndexOf(';') is var pv and >= 0) davantiAlValore = davantiAlValore[(pv + 1)..];
                    // Davanti valgono solo se l'elenco introduce il valore coi due punti; se no sono la coda della voce prima («…
                    // until passing 6000FT for FARAK 7Q and IRKED 8E - 7.5% …», LIMC).
                    var sid = sidDellaParte.Count > 0 ? sidDellaParte
                        : RxElencoCheIntroduce().IsMatch(davantiAlValore) && Sid(davantiAlValore) is { Count: > 0 } prima ? prima : Sid(coda);
                    var ft = Numero(v.Groups["ft"]) ?? Numero(v.Groups["ft2"]) ?? Numero(v.Groups["ft3"]);
                    var pc = Numero(v.Groups["pc"]) ?? Numero(v.Groups["pc2"]) ?? Numero(v.Groups["pc3"]);
                    if (ft is null && pc is null) continue;
                    var fino = RxFinoA().Match(coda) is { Success: true } u ? RxSpazi().Replace(u.Groups["q"].Value, " ").Trim() : null;
                    var perche = Perche(coda) is var p && p != ClimbGradientReason.Unspecified ? p : perDefault;
                    var detto = RxSpazi().Replace((v.Value + coda).Trim(), " ");
                    lette.Add(new Letta(new ClimbGradient(ft, pc, fino, perche, detto.Length > 200 ? detto[..200] : detto), sid));
                    if (perLoro.Success)
                    {
                        var finoPerLoro = RxFinoA().Match(perLoro.Value) is { Success: true } q ? RxSpazi().Replace(q.Groups["q"].Value, " ").Trim() : fino;
                        var dettoPerLoro = RxSpazi().Replace($"{v.Value} {perLoro.Value}".Trim(), " ");
                        lette.Add(new Letta(new ClimbGradient(ft, pc, finoPerLoro, perche, dettoPerLoro), Sid(perLoro.Value)));
                    }
                }
            }
        }
        return lette;
    }

    /// <summary>
    /// Le pendenze che valgono per <paramref name="designatore"/>: quelle che la nominano e, se <paramref name="ancheSenzaNomi"/>,
    /// quelle che non nominano nessuno.
    /// </summary>
    public static IEnumerable<ClimbGradient> Per(IEnumerable<Letta> lette, string designatore, bool ancheSenzaNomi) =>
        lette.Where(l => l.Sid.Count == 0 ? ancheSenzaNomi : l.Sid.Any(s => Uguale(s, designatore))).Select(l => l.Pendenza);

    /// <summary>
    /// La pendenza con cui disegnare (committente, 23 ottobre): quella degli ostacoli quando c'è; se no, una sola senza motivo;
    /// se no niente (il 3,3% e la segnalazione li mette chi disegna).
    /// </summary>
    public static ClimbGradient? PerDisegnare(IReadOnlyList<ClimbGradient>? pendenze)
    {
        if (pendenze is not { Count: > 0 }) return null;
        if (pendenze.FirstOrDefault(p => p.Reason == ClimbGradientReason.Obstacles) is { } ostacoli) return ostacoli;
        var senza = pendenze.Where(p => p.Reason == ClimbGradientReason.Unspecified).ToList();
        // Più d'una senza motivo ma tutte dello stesso valore (fino a quote diverse, LICB OBAXU 5B): quel valore.
        if (senza.Count > 1 && senza.Select(PiediPerMiglio).Distinct().Count() == 1) return senza[0];
        return senza.Count == 1 ? senza[0] : pendenze.Count == 1 ? pendenze[0] : null;
    }

    /// <summary>I piedi per miglio di una pendenza: stampati, o dal per cento (6076 ft per miglio).</summary>
    public static double? PiediPerMiglio(ClimbGradient p) => p.FeetPerNm ?? (p.Percent is { } pc ? pc / 100 * 6076.12 : null);

    private static ClimbGradientReason Perche(string testo) =>
        RxOstacoli().IsMatch(testo) ? ClimbGradientReason.Obstacles : RxAtc().IsMatch(testo) ? ClimbGradientReason.Atc : ClimbGradientReason.Unspecified;

    private static bool Uguale(string a, string b) => RxSpazi().Replace(a, "") == RxSpazi().Replace(b, "");

    private static List<string> Sid(string testo) => [.. RxDesignatore().Matches(testo).Select(m => RxSpazi().Replace(m.Value, " "))];

    private static double? Numero(Group g) =>
        g.Success ? double.Parse(g.Value.Replace(',', '.'), CultureInfo.InvariantCulture) : null;

    [GeneratedRegex(@"\s+")]
    private static partial Regex RxSpazi();

    // Non «gradient of»: «(a gradient of 300 ft/NM … provides separation from obstacles)» fa parte della stessa voce (LIPO).
    [GeneratedRegex(@"\bclimb\s+gradients?\b|\bPDG\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxContesto();

    [GeneratedRegex(@"\(\s*for\s+(?:SIDs?\s+)?[A-Z]{3,5}\s?\d{1,2}[A-Z]\b[^)]*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxPerSidFraParentesi();

    [GeneratedRegex(@"\bFor\s+the\s+following\s+SIDs?\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxPerLeSeguenti();

    // «365 ft/NM - 6.0%», «425 ft/NM (7%)», «8.3% (500 ft/NM)», «8% (490 ft/NM)», «300 ft/NM», «7%».
    [GeneratedRegex(@"(?<ft>\d{3})\s*(?:ft|FT)\s*/\s*NM\s*[-–(]*\s*(?<pc>\d+(?:[.,]\d+)?)\s*%\)?|(?<pc2>\d+(?:[.,]\d+)?)\s*%\s*\(?\s*(?<ft2>\d{3})\s*(?:ft|FT)\s*/\s*NM\s*\)?|(?<ft3>\d{3})\s*(?:ft|FT)\s*/\s*NM|(?<!OIS\s*)(?<pc3>\d{1,2}(?:[.,]\d+)?)\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex RxValore();

    [GeneratedRegex(@"\b(?:WARNING|Warning|Close[\s-]in|Pilots\s+shall|Initial\s+climb\s+gradient)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxFineContesto();

    [GeneratedRegex(@"\bFor\s+(?:[A-Z]{3,5}\s?\d{1,2}[A-Z][\s,]*(?:and\s+)?)+(?:[Mm]inimum\s*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex RxPerDavanti();

    [GeneratedRegex(@";|\.\s+(?=[A-Z])|\bREMARK|\bthen\s*:|\s[a-e]\)", RegexOptions.CultureInvariant)]
    private static partial Regex RxFineVoce();

    [GeneratedRegex(@"\b(?:until|till|up\s+to)\s+(?:passing|leaving|reaching|crossing)?\s*(?<q>FL\s?\d{2,3}|\d{3,5}\s*(?:ft|FT)|Transition\s+Level|TRL|levels\s+reported\s+in\s+each\s+SID)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxFinoA();

    [GeneratedRegex(@"\bobstacles?\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxOstacoli();

    [GeneratedRegex(@"\bATC\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxAtc();

    [GeneratedRegex(@"\b[A-Z]{3,5}\s?\d{1,2}[A-Z]\b", RegexOptions.CultureInvariant)]
    private static partial Regex RxDesignatore();

    // L'elenco delle SID che introduce un valore: finisce coi due punti, o con l'ultima SID attaccata al valore («b) if
    // proceeding via SID TUVDE 7A 515 ft/NM», LIPX).
    [GeneratedRegex(@"(?::|\b[A-Z]{3,5}\s?\d{1,2}[A-Z])\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex RxElencoCheIntroduce();
}
