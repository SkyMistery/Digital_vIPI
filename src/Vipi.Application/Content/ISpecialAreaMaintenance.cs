namespace Vipi.Application.Content;

/// <summary>
/// Riconciliazione one-shot delle aree regolamentate, eseguita all'avvio dopo la migrazione dello schema.
/// <b>Idempotente</b>: rieseguirla non cambia nulla. Sta qui e non in una migrazione EF perché le migrazioni del
/// repo sono SQLite-flavored, mentre il deploy hostato crea lo schema col <c>PostgresSchemaReconciler</c>: un
/// backfill scritto in SQL di migrazione non girerebbe in produzione (stesso motivo di
/// <see cref="IDocumentMaintenance"/>).
/// </summary>
public interface ISpecialAreaMaintenance
{
    /// <summary>
    /// Porta l'appartenenza area→ACC dalla vecchia colonna singola <c>SpecialAreas.CenterId</c> alla tabella dei
    /// legami, e poi si sbarazza della colonna. Ritorna il numero di legami creati (0 se già fatto).
    /// <para>
    /// Recupera UNA sola appartenenza per area — quella che il vecchio import aveva lasciato vincere — perché è
    /// tutto ciò che il modello precedente sapeva. Le altre le riporta il primo import successivo.
    /// </para>
    /// </summary>
    Task<int> BackfillAreaCentersAsync(CancellationToken ct = default);

    /// <summary>
    /// Una tantum: spegne l'import delle aree per tutti gli ACC <b>esteri</b> e libera l'archivio dalle loro aree
    /// (restano quelle che un ente abilitato elenca ancora). Ritorna quanti legami ha tolto.
    /// <para>
    /// Gira una volta sola — il «già fatto» è una riga di <c>ImportState</c> — altrimenti a ogni riavvio
    /// cancellerebbe le aree di un ACC estero appena abilitato a mano dall'admin.
    /// </para>
    /// </summary>
    Task<int> OptOutForeignAreasAsync(CancellationToken ct = default);
}
