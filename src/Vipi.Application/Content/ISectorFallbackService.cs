namespace Vipi.Application.Content;

/// <summary>Una riga di ripiego come si edita: bersaglio e fascia. L'ordine lo dà la posizione nella lista.</summary>
public sealed record FallbackRowEdit(string TargetCallsign, int? BaseFeet, int? TopFeet);

/// <summary>
/// La catena di ripiego <b>dichiarata</b> di un settore: leggerla, riscriverla, e farsela proporre dalla
/// geometria.
///
/// <para>⚠️ <b>Non è una seconda gerarchia.</b> Queste righe stanno DAVANTI al padre di copertura nella stessa
/// catena, non accanto: dove finiscono, la ricaduta prosegue sul padre come ha sempre fatto. Un settore senza
/// righe si comporta esattamente come prima che questa tabella esistesse. Carta
/// <c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §2.</para>
/// </summary>
public interface ISectorFallbackService
{
    /// <summary>Le righe dichiarate di un settore, in ordine.</summary>
    Task<IReadOnlyList<FallbackRowEdit>> ListAsync(string sectorCallsign, CancellationToken ct = default);

    /// <summary>
    /// Sostituisce <b>tutte</b> le righe di un settore con quelle date (lista vuota = nessun ripiego
    /// dichiarato, cioè ricaduta per soli padri). Sostituzione e non modifica riga per riga: l'ordine è parte
    /// del significato, e riscriverlo per intero è l'unico modo di non doverlo ricucire.
    /// </summary>
    Task ReplaceAsync(string sectorCallsign, IReadOnlyList<FallbackRowEdit> rows, CancellationToken ct = default);

    /// <summary>
    /// Cosa propone la geometria per quel settore, dal più sovrapposto in quota al meno. <b>Solo una
    /// proposta</b>: non scrive niente, e non è mai il sistema a decidere una ricaduta da sé.
    /// </summary>
    Task<IReadOnlyList<FallbackSuggestion>> SuggestAsync(string sectorCallsign, CancellationToken ct = default);
}
