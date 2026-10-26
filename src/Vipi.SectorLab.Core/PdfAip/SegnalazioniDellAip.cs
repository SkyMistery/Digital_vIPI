using System.Text.RegularExpressions;
using Aip;
using Aip.Pdf.Italia;

namespace Vipi.SectorLab.Core.PdfAip;

/// <summary>
/// Le segnalazioni della lettura divise per quel che chiedono (26 ottobre, committente: «le segnalazioni come si correggono?
/// Almeno quelle che posso correggere io»). Delle 306 del 2611 solo qualche decina chiede qualcosa all'AOD; le altre dicono
/// come il Lab ha letto.
/// </summary>
public enum GruppoDellaSegnalazione
{
    /// <summary>Da sistemare: un dato che manca (AJO), una scelta (Magra, T001645, JESSY), un'area o un conto che non torna.</summary>
    DaSistemare,

    /// <summary>Un errore di stampa dell'AIP che il Lab ha già letto e dice come: da guardare, e da segnalare a ENAV.</summary>
    ErroreDellAip,

    /// <summary>Una nota della lettura: la procedura è letta e disegnata, e il Lab dice dove ha approssimato.</summary>
    Nota,
}

/// <summary>Il gruppo di una segnalazione, il dato che le manca e la voce dell'Indice a cui porta.</summary>
public static partial class SegnalazioniDellAip
{
    /// <summary>Il gruppo, dal tipo e dalle parole del dettaglio.</summary>
    public static GruppoDellaSegnalazione Gruppo(ImportIssue problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        var dettaglio = problema.Detail ?? "";
        if (dettaglio.Contains("errore di stampa", StringComparison.Ordinal))
            return GruppoDellaSegnalazione.ErroreDellAip;
        return problema.Kind switch
        {
            ImportIssueKind.PageWithoutTable or ImportIssueKind.LimitsFromText => GruppoDellaSegnalazione.Nota,
            // Solo la prosa letta: una cella di tabella che non si legge (LIMC RNP Y RWY17L, «distanza «/»») è da sistemare.
            ImportIssueKind.Procedure when dettaglio.StartsWith("prosa letta con", StringComparison.Ordinal) => GruppoDellaSegnalazione.Nota,
            _ => GruppoDellaSegnalazione.DaSistemare,
        };
    }

    /// <summary>
    /// Il dato che manca all'AIP espanso perché la procedura si legga: «AJO: non è una radioassistenza dell'AIP» → una
    /// radioassistenza AJO (la stazione straniera delle STAR di LIEA e LIEO); «XY: non è un punto né una radioassistenza» → un
    /// punto. Null se la segnalazione non dice un nome che manca.
    /// </summary>
    public static (CorrectionKind Tipo, string Nome)? DatoMancante(ImportIssue problema)
    {
        ArgumentNullException.ThrowIfNull(problema);
        if (problema.Detail is not { } dettaglio || RxNomeMancante().Match(dettaglio) is not { Success: true } m)
            return null;
        return (m.Groups["punto"].Success ? CorrectionKind.Point : CorrectionKind.Navaid, m.Groups["nome"].Value);
    }

    /// <summary>
    /// La voce dell'Indice di cui parla la segnalazione (il suo indice nello strato), o null. La chiave della segnalazione è
    /// quella della voce (procedure, punti, radioassistenze, ostacoli); per le attese e le aree basta il nome davanti o in coda
    /// (JESSY → «JESSY 034 right»).
    /// </summary>
    public static int? VoceDi(ImportIssue problema, IReadOnlyList<VoceDellAip> voci)
    {
        ArgumentNullException.ThrowIfNull(problema);
        ArgumentNullException.ThrowIfNull(voci);
        if (string.IsNullOrWhiteSpace(problema.Key))
            return null;
        var chiave = problema.Key.Trim();
        var esatta = Trova(v => string.Equals(v.Chiave, chiave, StringComparison.Ordinal));
        return esatta ?? Trova(v => v.Chiave is { } c
            && (c.StartsWith(chiave + " ", StringComparison.Ordinal) || c.EndsWith(" " + chiave, StringComparison.Ordinal) || c.Contains(" " + chiave + " ", StringComparison.Ordinal)));

        int? Trova(Func<VoceDellAip, bool> va)
        {
            for (var i = 0; i < voci.Count; i++)
                if (va(voci[i])) return i;
            return null;
        }
    }

    [GeneratedRegex(@"\bprosa non letta:\s*(?<nome>[A-Z0-9]{2,5}):\s*non è (?:(?<punto>un punto né una radioassistenza)|una radioassistenza) dell'AIP", RegexOptions.CultureInvariant)]
    private static partial Regex RxNomeMancante();
}
