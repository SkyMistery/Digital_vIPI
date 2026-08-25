namespace Vipi.Application.Content;

/// <summary>
/// Riconciliazione one-shot dei cataloghi settori, eseguita all'avvio dopo la migrazione dello schema.
/// <b>Idempotente</b>, e per la stessa ragione di <see cref="ISpecialAreaMaintenance"/> non sta in una
/// migrazione EF: quelle del repo sono SQLite-flavored, mentre il deploy hostato crea lo schema col
/// <c>PostgresSchemaReconciler</c>.
/// </summary>
public interface ISectorCatalogMaintenance
{
    /// <summary>
    /// Marca come <b>aggiunte a mano</b> le righe di catalogo ACC che la sorgente non ha mai mandato, così il
    /// controllo del timbro non le scambia per «sparite dalla sorgente».
    ///
    /// <para><b>Come le riconosce</b>: un subcenter di un ACC porta il codice di quell'ACC nel proprio callsign
    /// (<c>LIRR_TS_CTR</c> in LIRR). Le righe aggiunte a mano dalla pagina Confinanti sono invece <b>APP
    /// d'aeroporto</b> catalogati sotto l'ACC estero che li ospita — <c>LGKR_APP</c> sotto LGGG — e quel
    /// prefisso non combacia mai. Misurato sull'archivio del 25 agosto 2026: cinque righe, cinque manuali,
    /// nessun falso positivo.</para>
    ///
    /// <para>Gira una volta sola (il «già fatto» è una riga di <c>ImportState</c>): da qui in avanti il segno
    /// lo mette chi le crea, e rifarlo a ogni riavvio marcherebbe a mano anche righe che nel frattempo la
    /// sorgente ha cominciato a mandare.</para>
    /// </summary>
    Task<int> MarkManualCatalogRowsAsync(CancellationToken ct = default);
}
