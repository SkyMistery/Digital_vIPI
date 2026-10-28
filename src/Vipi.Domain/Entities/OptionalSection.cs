namespace Vipi.Domain.Entities;

/// <summary>
/// Una sezione di catalogo che un admin ha dichiarato <b>facoltativa</b> per un profilo di documento (S133, carta
/// <c>docs/feature/2026-10-28-stato-dei-documenti.md</c> §1 D4): vuota non conta fra le sezioni da scrivere. La regola
/// di partenza resta nel codice (<c>SezioniDaScrivere.DaScrivere</c>); questa tabella la restringe, sezione per sezione.
/// <para>Una riga c'è = facoltativa. Toglierla la rende di nuovo da scrivere.</para>
/// </summary>
public class OptionalSection
{
    public int Id { get; set; }

    /// <summary>Il profilo del catalogo (<c>SectionProfile</c>), per nome: un profilo nuovo non sposta i numeri.</summary>
    public string Profile { get; set; } = "";

    /// <summary>La chiave della sezione nel catalogo di quel profilo.</summary>
    public string SectionKey { get; set; } = "";

    public DateTime SetUtc { get; set; }
    public int SetByUserId { get; set; }
}
