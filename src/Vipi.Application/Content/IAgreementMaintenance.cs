namespace Vipi.Application.Content;

/// <summary>
/// Il **travaso** dei flussi storici negli accordi di coordinamento: una passata sola, all'avvio, come le altre
/// riconciliazioni one-shot del progetto.
/// <para>La regola di conversione sta in <see cref="FlowsToAgreements"/> ed è pura — qui c'è solo il giro sui
/// dati, il segnaposto «già fatto» e la scrittura. Separarli è ciò che permette di provare il travaso sui 78
/// punti veri senza un database.</para>
/// </summary>
public interface IAgreementMaintenance
{
    /// <summary>
    /// Converte tutti i flussi in accordi, una volta sola. Ritorna quanti accordi ha creato; 0 se il travaso era
    /// già stato fatto o se non c'era niente da travasare.
    /// <para>I flussi <b>non</b> vengono cancellati: la loro tabella sparisce con la migrazione che chiude il
    /// lavoro, e finché c'è resta la sola copia di riferimento con cui confrontare il risultato.</para>
    /// </summary>
    Task<int> MigrateFlowsToAgreementsAsync(CancellationToken ct = default);
}
