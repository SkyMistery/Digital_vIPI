namespace Vipi.Domain.Entities;

/// <summary>
/// Una voce del <b>glossario di fraseologia</b>: una frase che si dice in un modo solo, e come si dice
/// (<c>lavori-aperti §Q3</c>, carta <c>2026-08-27-documenti-bilingue.md</c> §5).
///
/// <para>
/// ⚠️ <b>Perché è una tabella e non una lista nel codice.</b> La domanda aperta di §Q3 non è «quali frasi»:
/// è <b>chi</b>. Finché il glossario si cura ricompilando <c>TitoliUfficiali.cs</c>, l'unica persona che può
/// curarlo è chi scrive il codice — cioè esattamente quella che la carta dice non debba farlo, perché sapere
/// quale forma è standard è mestiere di un controllore. La tabella non aggiunge una funzione: sposta la
/// competenza dove sta.
/// </para>
///
/// <para>
/// ⚠️ <b>Non è la memoria di traduzione, e le due non si somigliano.</b> Un <c>TranslationUnit</c> è un
/// <b>segmento intero</b> tradotto — un titolo, una cella, un paragrafo — e si trova per impronta esatta.
/// Una voce di glossario è un <b>pezzo di frase</b>, si trova per sottostringa a parola intera, e non
/// traduce niente da sola: dice solo come va reso quel pezzo dentro qualunque frase lo contenga.
/// </para>
/// </summary>
public class GlossaryTerm
{
    public int Id { get; set; }

    /// <summary>Lingua della frase sorgente (<c>it</c>, <c>en</c>). ⚠️ Il glossario è <b>direzionale</b>:
    /// «riporta sottovento → report downwind» non si legge al contrario, perché il verso inverso di una
    /// formula standard è un'altra formula standard, non la stessa girata.</summary>
    public string SourceLang { get; set; } = default!;

    /// <summary>Lingua della resa.</summary>
    public string TargetLang { get; set; } = default!;

    /// <summary>La frase come l'ha scritta chi cura il glossario. È questa che si mostra e si cerca.</summary>
    public string SourceText { get; set; } = default!;

    /// <summary>
    /// <see cref="SourceText"/> in minuscolo: serve <b>solo</b> all'indice unico.
    /// <para>⚠️ Non è una ridondanza da togliere. La ricerca nel testo è senza distinzione di maiuscole, e
    /// due voci che differiscono solo per quelle sono la stessa voce con due rese — ne vincerebbe una a caso.
    /// L'unicità va imposta dal database, e il database non è d'accordo con sé stesso su cosa sia una
    /// maiuscola: MySQL confronta senza distinguerle, SQLite distinguendole. Una colonna già in minuscolo è
    /// l'unico modo perché la regola sia la stessa dove il sito gira davvero e dove girano i test.</para>
    /// </summary>
    public string SourceKey { get; set; } = default!;

    /// <summary>Quello che va scritto al posto della frase, <b>verbatim</b>.</summary>
    public string TargetText { get; set; } = default!;

    public DateTime CreatedUtc { get; set; }

    /// <summary>Quando l'ha toccata l'ultima volta una persona. null = com'è nata.</summary>
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>VID di chi l'ha scritta o corretta. ⚠️ Resta <b>qui</b>: è un dato personale e non esce mai
    /// verso il motore — del resto il motore il glossario non lo vede nemmeno.</summary>
    public int? UpdatedByUserId { get; set; }
}
