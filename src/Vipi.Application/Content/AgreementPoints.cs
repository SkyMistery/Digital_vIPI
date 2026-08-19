using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// I punti d'ingresso di un accordo, per verso — e quali stanno da un lato solo.
///
/// <para>La domanda «gli stessi punti nei due sensi?» è quella che ha prodotto le asimmetrie vere
/// dell'archivio (<c>LIBB→LGGG</c> elenca BELIX, <c>LGGG→LIBB</c> elenca OLGAT), e si fa in <b>due</b> posti:
/// il cruscotto delle lacune, che confronta versi scritti anche in accordi diversi, e il riquadro di lavoro, che
/// mette i due versi di un accordo uno sotto l'altro. Un conto solo, letto due volte: due conti che dicono la
/// stessa cosa sono due occasioni di farli divergere.</para>
/// </summary>
public static class AgreementPoints
{
    /// <summary>I punti di un verso, senza ripetizioni e senza il segnaposto vuoto che <see cref="CopList.Parse"/>
    /// restituisce per una clausola senza punti indicati.</summary>
    public static HashSet<string> Of(AgreementRow a, AgreementDirection direction) =>
        a.Sections.Where(s => s.Direction == direction).SelectMany(s => s.Clauses)
            .SelectMany(c => CopList.Parse(c.Cops))
            .Where(p => p.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// I punti che <b>non</b> stanno in tutti i versi dati, in ordine alfabetico. Quelli comuni sono l'accordo
    /// che regge e non vanno segnalati.
    /// </summary>
    /// <remarks>Con meno di due versi non c'è niente da confrontare: un verso solo non è un'asimmetria, è un
    /// reciproco da scrivere — e quello lo dice il conteggio delle clausole, non questo.</remarks>
    public static IReadOnlyList<string> Unpaired(IReadOnlyList<IReadOnlySet<string>> perDirection)
    {
        if (perDirection.Count < 2 || perDirection.Any(s => s.Count == 0)) return Array.Empty<string>();

        return perDirection.SelectMany(s => s)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            .Where(pt => perDirection.Any(s => !s.Contains(pt)))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>I punti di una singola sezione.</summary>
    public static HashSet<string> Of(AgreementSectionRow s) =>
        s.Clauses.SelectMany(c => CopList.Parse(c.Cops))
            .Where(p => p.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>I punti spaiati fra i due versi di <b>un</b> accordo: la riga d'avviso in testa al riquadro.</summary>
    public static IReadOnlyList<string> UnpairedWithin(AgreementRow a) =>
        Unpaired(new IReadOnlySet<string>[]
        {
            Of(a, AgreementDirection.AtoB),
            Of(a, AgreementDirection.BtoA),
        });

    /// <summary>I punti spaiati fra due sezioni speculari (i due versi dei sorvoli): è lì che l'asimmetria si
    /// vede, ora che stanno nello stesso accordo una sotto l'altra.</summary>
    public static IReadOnlyList<string> UnpairedBetween(AgreementSectionRow x, AgreementSectionRow y) =>
        Unpaired(new IReadOnlySet<string>[] { Of(x), Of(y) });
}
