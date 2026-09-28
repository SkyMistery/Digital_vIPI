namespace Vipi.Application.Stats;

/// <summary>Che cosa ha fatto la passata una tantum sullo storico delle statistiche.</summary>
/// <param name="Turni">Sessioni a cui è cambiata la chiave di turno.</param>
/// <param name="Giorni">Giorni aeroporto già consolidati rimessi in coda al consolidamento.</param>
public sealed record StoricoRifatto(int Turni, int Giorni)
{
    public static StoricoRifatto Niente { get; } = new(0, 0);
}

/// <summary>
/// Le passate d'avvio sui dati delle statistiche.
/// </summary>
public interface IStatsMaintenance
{
    /// <summary>
    /// <b>Una volta sola</b> (registro <c>ImportCategories.StoricoStatistiche</c>): rifà lo storico con le regole
    /// corrette dalla revisione totale 3 — scelta del committente del 28 settembre 2026.
    /// <list type="bullet">
    ///   <item><b>U-218</b>: i turni dell'ultimo anno si ricalcolano col raggruppatore che tollera la sovrapposizione
    ///   di pochi secondi (circa 282 turni spezzati misurati dall'audit);</item>
    ///   <item><b>U-228</b>: i giorni aeroporto già consolidati si rimettono in coda (non si cancellano: il
    ///   consolidamento notturno li riprende dalla sorgente e li riscrive col conto nuovo, dal più recente, un blocco
    ///   alla volta). Un giorno che la sorgente non restituisce più resta com'era.</item>
    /// </list>
    /// </summary>
    Task<StoricoRifatto> RifaiStoricoAsync(CancellationToken ct = default);
}
