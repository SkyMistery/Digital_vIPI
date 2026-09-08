using System;
using System.Collections.Generic;
using System.Linq;

namespace Vipi.Application.Content;

/// <summary>
/// Una riga che sta in un **outline** di varianti: alternative pari-grado, eccezioni annidate a profondità
/// libera, e righe che scavalcano tutto. L'implementano sia la clausola di un accordo — che è dove l'outline
/// si scrive — sia la riga proiettata, che è dove si legge.
/// </summary>
// ⚠️ Pubblico perché compare nella FIRMA di un tipo pubblico: chi lo restringe scopre che il
// compilatore lo dice da sé (CS0050/CS0051/CS0053). È superficie del modulo quanto il tipo che lo
// espone (ADR-0005 D6, revisione del 6 settembre 2026, R-009).
public interface IOutlineRow
{
    int Id { get; }
    /// <summary>null = riga singola, fuori da ogni gruppo.</summary>
    int? VariantGroup { get; }
    /// <summary>0 = alternativa di primo livello, 1 = sua eccezione, 2 = eccezione dell'eccezione, …</summary>
    int VariantDepth { get; }
    /// <summary>La riga vale per tutto il gruppo: non appartiene a nessuna alternativa.</summary>
    bool IsGroupWide { get; }
}

/// <summary>
/// La lettura dell'outline: **l'ordine è la struttura**, e una riga appartiene all'ultima meno profonda che la
/// precede — come una lista puntata.
///
/// <para><b>Perché è generica.</b> La stessa risalita serve in tre posti — la composizione della frase, la
/// tabella dell'editor che deve dire di quale riga una condizione è l'eccezione, e l'anteprima — su due forme
/// diverse (la clausola in scrittura, la riga proiettata in lettura). Tre risalite scritte a mano potrebbero
/// leggere tre strutture diverse dallo stesso outline, e la differenza non si vedrebbe da nessuna parte:
/// un'eccezione attribuita all'alternativa sbagliata non è un errore, è un accordo che dice un'altra cosa.</para>
/// </summary>
public static class Outline
{
    /// <summary>
    /// La riga di cui <paramref name="row"/> è un'eccezione: la prima MENO PROFONDA che la precede nello stesso
    /// gruppo. <c>null</c> se non ce n'è una — fuori da un gruppo, a profondità 0 (le alternative sono
    /// pari-grado: nessuna è lo standard dell'altra) o su una riga che scavalca le alternative.
    /// </summary>
    public static T? ParentOf<T>(IReadOnlyList<T> rowsInOrder, T row) where T : class, IOutlineRow
    {
        if (row.VariantGroup is null || row.VariantDepth == 0 || row.IsGroupWide) return null;

        var i = IndexOf(rowsInOrder, row);
        for (var k = i - 1; k >= 0; k--)
        {
            var a = rowsInOrder[k];
            if (a.VariantGroup != row.VariantGroup) return null;   // fuori dal gruppo: la catena finisce qui
            if (a.VariantDepth >= row.VariantDepth) continue;      // pari-grado o più profonda: non è un antenato
            return a;
        }
        return null;
    }

    /// <summary>
    /// La catena delle condizioni che valgono per una riga: quelle dei suoi antenati nell'outline più la
    /// propria, dalla capofila in giù. Una riga fuori da un gruppo, o a profondità 0, ha solo la propria.
    /// <para>Una riga che scavalca le alternative NON eredita: vale per tutte, quindi non sta dentro nessuna.</para>
    /// </summary>
    public static IReadOnlyList<ConditionClause> ConditionChain<T>(
        IReadOnlyList<T> rowsInOrder, T row, Func<T, ConditionClause> condition) where T : class, IOutlineRow
    {
        var chain = new List<ConditionClause> { condition(row) };
        for (var a = ParentOf(rowsInOrder, row); a is not null; a = ParentOf(rowsInOrder, a))
            chain.Insert(0, condition(a));
        return chain;
    }

    /// <summary>
    /// La riga <b>e tutto ciò che pende da lei</b>: le sue eccezioni, le eccezioni delle eccezioni, a
    /// qualunque profondità. Una riga senza figli torna da sola, e la riga stessa è sempre la prima.
    ///
    /// <para>🔴 <b>Si costruisce risalendo, non scendendo</b>, ed è la sola cosa che conta qui: figlia è la
    /// riga la cui catena di <see cref="ParentOf"/> passa da questa. Scendendo — «le righe che seguono, più
    /// profonde, finché non ne arriva una meno profonda» — si scriverebbe una <b>seconda</b> lettura
    /// dell'outline, e due letture dello stesso outline prima o poi dicono due alberi diversi senza che la
    /// differenza si veda da nessuna parte. È la ragione per cui questa classe esiste, scritta in testa.</para>
    ///
    /// <para>⚠️ Chi <b>scavalca</b> le alternative non pende da nessuno (<see cref="IOutlineRow.IsGroupWide"/>
    /// non ha antenati, per costruzione di <see cref="ParentOf"/>): vale per tutto il gruppo, quindi non se ne
    /// va con una sola alternativa. Cade solo se si elimina lei.</para>
    ///
    /// <para>Serve a eliminare: una riga che se ne va lasciando indietro le proprie eccezioni non lascia
    /// «righe in più», lascia righe che <b>dicono un'altra cosa</b> — l'eccezione di una condizione che non
    /// c'è più diventa una clausola qualunque.</para>
    /// </summary>
    public static IReadOnlyList<T> SubtreeOf<T>(IReadOnlyList<T> rowsInOrder, T row) where T : class, IOutlineRow
    {
        var sottoalbero = new List<T> { row };
        var dentro = new HashSet<int> { row.Id };

        // L'ordine è la struttura: un antenato PRECEDE sempre il suo discendente, quindi una sola passata in
        // avanti basta — quando si guarda una riga, chi sta sopra di lei è già stato deciso.
        foreach (var candidata in rowsInOrder)
        {
            if (dentro.Contains(candidata.Id)) continue;
            var padre = ParentOf(rowsInOrder, candidata);
            if (padre is not null && dentro.Contains(padre.Id))
            {
                sottoalbero.Add(candidata);
                dentro.Add(candidata.Id);
            }
        }
        return sottoalbero;
    }

    private static int IndexOf<T>(IReadOnlyList<T> rows, T row) where T : class, IOutlineRow
    {
        for (var i = 0; i < rows.Count; i++)
            if (rows[i].Id == row.Id) return i;
        return -1;
    }
}
