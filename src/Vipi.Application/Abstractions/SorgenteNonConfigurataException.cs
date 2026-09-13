namespace Vipi.Application.Abstractions;

/// <summary>
/// La sorgente non si può interrogare perché <b>non è configurata</b> (niente credenziali): non è un guasto, è
/// un'installazione senza quella sorgente.
///
/// <para>🔴 <b>Perché un tipo suo (T-006, revisione del 13 settembre 2026).</b> I giri automatici d'import
/// catturavano <see cref="InvalidOperationException"/> come «credenziali assenti» e rispondevano «riuscito». Ma
/// la stessa eccezione la sollevano i client anche per un <b>503</b> o un <b>403</b> di IVAO: il giro fallito
/// veniva timbrato come riuscito, niente retry dopo un'ora, la pagina Sorgenti verde, e dopo due notti la
/// soglia di eliminazione autorizzava a togliere tutto ciò che non era stato riletto.</para>
///
/// <para>⚠️ <b>Sottoclasse</b> di <see cref="InvalidOperationException"/> apposta: le pagine che lanciano
/// l'import a mano mostrano il messaggio di entrambe allo stesso modo, e non devono cambiare. Sono i giri
/// automatici a dover distinguere, e catturano <b>solo</b> questa.</para>
/// </summary>
public sealed class SorgenteNonConfigurataException : InvalidOperationException
{
    public SorgenteNonConfigurataException(string message) : base(message) { }
}
