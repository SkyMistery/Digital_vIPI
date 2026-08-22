using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Content;

/// <summary>
/// L'elenco dei punti d'ingresso di una clausola, salvato in **una colonna sola** con un separatore — come
/// <c>ConditionLabel</c> fa già per le multi-pista.
///
/// <para><b>Perché una stringa e non una tabella figlia.</b> Il CoP è testo libero con validazione soft (un fix,
/// un intervallo di aerovie, una STAR, «ALL»), non un riferimento: una tabella figlia aggiungerebbe una join e
/// un'entità per contenere stringhe che nessuno mette in relazione con nient'altro. La cella dell'editor scrive
/// già testo, e resta a scrivere testo.</para>
///
/// <para><b>Il separatore è la virgola</b> e non il punto mediano: i token reali in archivio non ne contengono
/// (<c>Y01-Y12</c>, <c>TOPNO 3A</c>, <c>ALL to GR</c>), la virgola è ciò che si digita di istinto elencando, e
/// il punto mediano è una scelta di RESA — la tabella può separare come vuole senza che il dato cambi.</para>
/// </summary>
public static class CopList
{
    private const char Separator = ',';

    /// <summary>I punti dell'elenco, nell'ordine scritto, senza vuoti. Un elenco senza nessun punto restituisce
    /// **un elemento vuoto**: «nessun punto indicato» è un caso che la frase sa dire (<c>FallbackMissingPoint</c>,
    /// il «—»), e farlo sparire qui trasformerebbe una clausola incompleta in una clausola assente.</summary>
    public static IReadOnlyList<string> Parse(string? raw)
    {
        var parts = (raw ?? "")
            .Split(Separator)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();
        return parts.Count > 0 ? parts : new List<string> { "" };
    }

    /// <summary>La forma salvata di un elenco di punti.</summary>
    public static string Format(IEnumerable<string?> cops) =>
        string.Join($"{Separator} ", cops.Select(c => (c ?? "").Trim()).Where(c => c.Length > 0));

    /// <summary>Un elenco di uno: la forma che ha ogni clausola nata dalla conversione di una riga vecchia.</summary>
    public static string One(string? cop) => (cop ?? "").Trim();

    /// <summary>Quanti punti porta l'elenco, contando il caso vuoto come uno (vedi <see cref="Parse"/>).</summary>
    public static int Count(string? raw) => Parse(raw).Count;

    /// <summary>
    /// Il token che si sta <b>scrivendo</b>: quello dopo l'ultima virgola, ripulito. Serve a chi propone —
    /// dentro «VALMA, EL» il nome da completare è «EL», non tutta la riga.
    /// </summary>
    public static string LastToken(string? raw)
    {
        var t = raw ?? "";
        var cut = t.LastIndexOf(Separator);
        return (cut < 0 ? t : t[(cut + 1)..]).Trim();
    }

    /// <summary>
    /// L'elenco con l'ultimo token sostituito dal punto scelto. È l'altra metà di <see cref="LastToken"/>: chi
    /// sceglie da un elenco a discesa sta completando UNA voce, non riscrivendo la riga.
    /// </summary>
    /// <remarks>Non aggiunge la virgola in coda. La stringa che esce di qui è quella che si SALVA, e una virgola
    /// finale finirebbe nel dato — <see cref="Parse"/> la ignorerebbe, ma chi rilegge la colonna no.</remarks>
    public static string ReplaceLastToken(string? raw, string? picked)
    {
        var t = raw ?? "";
        var cut = t.LastIndexOf(Separator);
        var head = cut < 0 ? "" : t[..(cut + 1)] + " ";
        return head + (picked ?? "").Trim();
    }

    /// <summary>Vero se i due elenchi contengono gli stessi punti nello stesso ordine. Serve all'invariante del
    /// gruppo di varianti: le clausole di un gruppo condividono i punti, perché sono lo stesso accordo detto a
    /// condizioni diverse.</summary>
    public static bool SameAs(string? a, string? b) =>
        Parse(a).SequenceEqual(Parse(b), StringComparer.OrdinalIgnoreCase);
}
