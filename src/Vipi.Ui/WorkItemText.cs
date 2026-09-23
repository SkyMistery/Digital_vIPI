using Microsoft.Extensions.Localization;
using Vipi.Application.Content;

namespace Vipi.Ui;

/// <summary>
/// Le parole di una riga di «Da fare»: la pastiglia e la frase. Stanno qui e non dentro <c>WorkItemRow</c> perché
/// dal 23 settembre 2026 le usa anche la <b>testata di un gruppo</b> (<c>WorkItemList</c>), che è la stessa frase
/// della sua riga più urgente: due composizioni della stessa frase sono due racconti che iniziano a divergere.
/// </summary>
public static class WorkItemText
{
    /// <summary>La pastiglia dice in una parola PERCHÉ la riga è lì, e il colore quanto urge: classe CSS,
    /// chiave del testo, chiave del suggerimento.</summary>
    public static (string Classe, string Chiave, string Titolo) Pastiglia(WorkSeverity s) => s switch
    {
        WorkSeverity.GiaInPubblico => ("red", "Impact_PublicNow", "Impact_PublicNowTitle"),
        WorkSeverity.Rotto => ("amber", "Impact_Broken", "Impact_Broken"),
        WorkSeverity.InRitardo => ("amber", "Tasks_Overdue", "Tasks_Overdue"),
        WorkSeverity.DaRipubblicare => ("blue", "Impact_Republish", "Impact_RepublishTitle"),
        // ⚠️ Il colore è lo stesso della riga precedente e la parola no: l'atto che le chiude è lo stesso — si
        // pubblica — ma qui non è rotto niente, c'è una SCADENZA. Un colore più acceso direbbe che urge come
        // quella sopra, uno spento che si può ignorare.
        WorkSeverity.DaPreparare => ("blue", "Impact_Prepare", "Impact_PrepareTitle"),
        WorkSeverity.DaRileggere => ("", "Impact_ToReview", "Impact_ToReview"),
        _ => ("", "Work_Task", "Work_Task"),
    };

    /// <summary>
    /// La frase della riga: chiave + argomenti, mai testo salvato (una riga scritta in italiano si
    /// ripresenterebbe in italiano a chi legge in inglese).
    ///
    /// <para>⚠️ Gli argomenti si passano <b>tutti</b>. Lo <c>switch</c> che stava nella riga si fermava a due e
    /// l'ultimo ramo buttava via il resto: una frase con tre segnaposto — «l'aeroporto {0} è passato da {1} a
    /// {2}» — alzava <c>FormatException</c> e, siccome la riga si disegna dentro il banner dell'editor, non
    /// rompeva la riga: <b>non faceva partire la pagina</b>. Preso guidando l'editor d'aeroporto il 5 settembre
    /// 2026, con la suite verde.</para>
    /// </summary>
    public static string Frase(WorkItem item, IStringLocalizer l) =>
        item.FraseKey == WorkPhrases.Raw
            ? (item.FraseArgs.Count > 0 ? item.FraseArgs[0] : "")
            : item.FraseArgs.Count == 0
                ? l[item.FraseKey].Value
                : l[item.FraseKey, item.FraseArgs.Cast<object>().ToArray()].Value;
}
