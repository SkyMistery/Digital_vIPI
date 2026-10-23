using System.Text.RegularExpressions;

namespace Aip.Pdf.Italia;

/// <summary>
/// Passo 4 della revisione col committente delle segnalazioni del 2611 (carta §6, SC1 e SC2): le salite iniziali. Una SID che
/// non parte dalla pista («At 13NM PRU DME turn right…») è il seguito della salita iniziale della sua pista («After take off
/// turn right until joining RDL193 PRU VOR (TR 193°) until 13NM PRU DME»): si leggono insieme. La salita può avere rami per
/// SID («a) on TR 262° if cleared via SID EKTOL 6A; b) …», «- if cleared for SID INKIM 9G or OTRET 8G: …», «1. If cleared for
/// SID KOVAS 7A or CAR 7A, after take off …»): vale il ramo che nomina la SID, dopo la parte comune.
/// </summary>
public static partial class ItalianAd2Prose
{
    /// <summary>Una salita iniziale divisa: la parte comune, e i rami con le SID che nominano.</summary>
    internal sealed record SalitaDivisa(string Comune, IReadOnlyList<(string Condizione, string Testo)> Rami);

    /// <summary>
    /// La salita iniziale divisa in parte comune e rami. Un ramo comincia con «a)», «A)», «- if», «1. If»; la condizione è
    /// la frase «if cleared via SID …» (davanti o in coda) o l'elenco delle SID coi due punti («TROTA 6J:», «SID AMLON 9B:»),
    /// il testo è il resto fino alla fine della frase.
    /// </summary>
    internal static SalitaDivisa Dividi(string descrizione)
    {
        // Gli a capo restano (il percorso finisce dove cominciano le righe delle quote e delle note); quel che sta prima del
        // decollo sono avvertenze e pendenze («WARNING: … OIS 2.5% …», LIBG, LICG, LIMZ).
        var testo = RxSpaziInRiga().Replace(RxTitoloDellaSalita().Replace(descrizione.Trim(), ""), " ");
        // Il testo comincia dal decollo, o dal primo ramo se viene prima («REMARK … A) If cleared via SID IBCUC 1M, after take-off
        // …», LIMC RWY 17L/17R).
        var primoRamo = RxInizioDiRamo().Match(testo);
        var decollo = RxDopoIlDecollo().Match(testo);
        var inizio = new[] { primoRamo, decollo }.Where(m => m.Success).Select(m => m.Index).DefaultIfEmpty(0).Min();
        if (inizio > 0) testo = testo[inizio..];
        var inizi = RxInizioDiRamo().Matches(testo).Select(m => m.Index).ToList();
        if (inizi.Count == 0) return new SalitaDivisa(SenzaLaSidAssegnata(testo), []);
        var rami = new List<(string, string)>();
        for (var i = 0; i < inizi.Count; i++)
        {
            var pezzo = testo[inizi[i]..(i + 1 < inizi.Count ? inizi[i + 1] : testo.Length)];
            pezzo = RxSegnoDiRamo().Replace(pezzo, "");
            var condizione = "";
            pezzo = RxCondizioneDavanti().Replace(pezzo, m => { condizione = m.Value; return ""; }, 1);
            pezzo = RxElencoColDuePunti().Replace(pezzo, m => { condizione += " " + m.Value; return ""; }, 1);
            pezzo = RxCondizioneInCoda().Replace(pezzo, m => { condizione += " " + m.Value; return ". "; }, 1);
            // Il ramo finisce con la sua frase: dopo vengono le note («Turn and climb parameters: …»).
            if (RxFineFrase().Match(pezzo) is { Success: true } fine) pezzo = pezzo[..fine.Index];
            // Un elenco senza SID («Turn and climb parameters: a) Departure turn limited to 210 KT; b) …») non è un ramo.
            if (condizione.Trim().Length > 0) rami.Add((condizione, SenzaLaSidAssegnata(pezzo)));
        }
        return new SalitaDivisa(SenzaLaSidAssegnata(RxCondizioneSenzaElenco().Replace(testo[..inizi[0]], " ")), rami);
    }

    /// <summary>
    /// «then join the assigned SID», «then proceed according to the assigned SID», «to join assigned SID as reported below»:
    /// la salita finisce, la SID continua. La virata detta lì resta (è quella con cui comincia la SID).
    /// </summary>
    internal static string SenzaLaSidAssegnata(string testo) =>
        RxSidAssegnata().Replace(testo, m => m.Groups["v"].Success ? $" {m.Groups["v"].Value}. " : ". ").Replace(':', ' ').Trim();

    internal const string NessunaSalita = "la pista non ha una salita iniziale da cui la SID continua";

    /// <summary>
    /// Una salita iniziale col percorso: «After take-off …» (anche dentro un ramo). Le altre sono avvertenze e pendenze («…
    /// require a suitable RWY for take-off», LIRF RWY 34R), e non si leggono.
    /// </summary>
    internal static bool HaUnPercorso(string? descrizione) => descrizione is not null && RxDopoIlDecollo().IsMatch(descrizione);

    /// <summary>Il nome di una SID nell'elenco di una condizione, senza badare agli spazi («BEROK8A» è BEROK 8A).</summary>
    private static bool Nomina(string condizione, string sid) =>
        RxSpazi().Replace(condizione, "").Contains(RxSpazi().Replace(sid, ""), StringComparison.Ordinal);

    /// <summary>
    /// Il testo della salita iniziale che vale per <paramref name="sid"/> sulla pista: la parte comune e il ramo che la nomina;
    /// se nessun ramo la nomina, la parte comune di una salita sola (o il ramo «if cleared for other SID»). Null col perché se
    /// non c'è o se ne valgono più d'una (SC1: «se nessuna o più d'una si segnala»).
    /// </summary>
    internal static (string? Testo, string? Perche) SalitaPerLaSid(string sid, IReadOnlyList<Procedure> salite, AipPage? pagina = null)
    {
        var tutte = salite.Where(s => HaUnPercorso(s.Description)).Select(s => (Salita: s, Divisa: Dividi(s.Description!))).ToList();
        if (tutte.Count == 0) return (null, NessunaSalita);
        var divise = tutte.Select(x => x.Divisa).ToList();
        var nominano = divise.SelectMany(d => d.Rami.Where(r => Nomina(r.Condizione, sid)).Select(r => $"{d.Comune} {r.Testo}")).ToList();
        if (nominano.Count == 1) return (nominano[0], null);
        if (nominano.Count > 1) return (null, "più salite iniziali nominano la SID");
        // Più salite per la stessa pista senza rami che la nominano (LIEA, LIEE): vale quella stampata sulla pagina della SID,
        // in testa alle SID che la continuano.
        if (tutte.Count > 1 && pagina?.Reference is { } pag && tutte.Where(x => x.Salita.Page?.Reference == pag).ToList() is [var stessa])
            divise = [stessa.Divisa];
        var altre = divise.SelectMany(d => d.Rami.Where(r => RxAltreSid().IsMatch(r.Condizione)).Select(r => $"{d.Comune} {r.Testo}")).ToList();
        if (altre.Count == 1) return (altre[0], null);
        var senzaRami = divise.Where(d => d.Rami.Count == 0).ToList();
        if (senzaRami.Count == 1 && divise.Count == 1) return (senzaRami[0].Comune, null);
        return (null, "la pista ha più salite iniziali e nessuna nomina la SID");
    }

    /// <summary>
    /// Una SID che non parte dalla pista: il testo comincia con «At N NM … DME», con una virata, con «Crossing RDL …», o con
    /// «Initial climb procedure completed» (SC1).
    /// </summary>
    internal static bool ContinuaLaSalita(string? descrizione) =>
        descrizione is not null && (RxDopoLaSalita().IsMatch(descrizione) || Route(descrizione) is { } t && RxComincaAMeta().IsMatch(t));

    [GeneratedRegex(@"(?<=^|[\s;:.,])(?:[a-eA-E]\)|[-–−]\s*(?=[Ii]f\b)|\d\.\s*(?=[Ii]f\b))", RegexOptions.CultureInvariant)]
    private static partial Regex RxInizioDiRamo();

    [GeneratedRegex(@"^\s*(?:[a-eA-E]\)|[-–−]|\d\.)\s*", RegexOptions.CultureInvariant)]
    private static partial Regex RxSegnoDiRamo();

    private const string ElencoDiSid = @"(?:SIDs?\s+)?(?:(?:[A-Z]{3,5}\s?\d{1,2}[A-Z]|other\s+SIDs?)(?:\s*(?:\([^)]*\))?\s*(?:,|\band\b|\bor\b|\be\b)\s*)?)+";

    [GeneratedRegex(@"^\s*[Ii]f\s+(?:cleared|proceeding|directed)\s+(?:for|via)\s+(?:the\s+following\s+)?" + ElencoDiSid + @"\s*[,:]?", RegexOptions.CultureInvariant)]
    private static partial Regex RxCondizioneDavanti();

    [GeneratedRegex(@"^\s*" + ElencoDiSid + @"(?:\s*\([^)]*\))?\s*:", RegexOptions.CultureInvariant)]
    private static partial Regex RxElencoColDuePunti();

    [GeneratedRegex(@"\s*\b[Ii]f\s+(?:cleared|proceeding|directed)\s+(?:for|via)\s+" + ElencoDiSid + @"\s*[;.]?", RegexOptions.CultureInvariant)]
    private static partial Regex RxCondizioneInCoda();

    // «After take-off proceed on track 313°, if cleared via SID: a) …», «then if cleared via: a) SID …».
    [GeneratedRegex(@",?\s*\b(?:then\s+)?if\s+cleared\s+via(?:\s+SID)?\s*:?\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxCondizioneSenzaElenco();

    [GeneratedRegex(@"\.\s+(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex RxFineFrase();

    [GeneratedRegex(@"\bother\s+SID", RegexOptions.CultureInvariant)]
    private static partial Regex RxAltreSid();

    [GeneratedRegex(@",?\s*\b(?:[Tt]hen\s+)?(?:(?<v>turn\s+(?:left|right))\s+)?(?:(?:until\s+joining|to\s+join|join(?:\s+and\s+follow)?|follow|proceed\s+(?:along|according\s+to))\s+)(?:the\s+)?assigned\s+SID\b(?:\s+as\s+reported\s+below)?\s*[:.]?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxSidAssegnata();

    [GeneratedRegex(@"^\s*(?:\([^)]*\)\s*)*Initial\s+climb\s+(?:procedure\s+)?(?:completed|executed)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxDopoLaSalita();

    [GeneratedRegex(@"[ \t]+", RegexOptions.CultureInvariant)]
    private static partial Regex RxSpaziInRiga();

    [GeneratedRegex(@"\bA\s?fter\s+(?:take[\s–-]?off|takeoff|TKOF)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxDopoIlDecollo();

    // «Initial climb description After take-off proceed …» (LIPE, LIPH, LICB): il titolo davanti al testo.
    [GeneratedRegex(@"^\s*Initial\s+climb\s+description\s*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex RxTitoloDellaSalita();

    // Anche da un punto (passo 5, committente: LIBP ASPIR 8G «Leave PES VOR on RDL 083…», che diventa «PES VOR - RDL 083…»;
    // «Proceed via BOA VOR/DME – LOMED», LIPE): la SID comincia dove la salita iniziale l'ha portata.
    [GeneratedRegex(@"^(?:\s*⟦[^⟧]*⟧)*\s*(?:At\s+\d|[Tt]urn\s|[Cc]rossing\s|[A-Z]{2,3}\s+(?:VOR|NDB)\b|Proceed\s+via\s+[A-Z]{2,5}\b)", RegexOptions.CultureInvariant)]
    private static partial Regex RxComincaAMeta();
}
