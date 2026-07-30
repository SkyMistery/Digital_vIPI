namespace Vipi.Application.Content;

/// <summary>
/// Riconciliazioni one-shot sui documenti, eseguite all'avvio dopo la migrazione dello schema (doc 11 §3a/§3c).
/// Sono **idempotenti**: rieseguirle non cambia nulla. Stanno qui e non in una migrazione EF perché le migrazioni
/// del repo sono SQLite-flavored, mentre il deploy hostato crea lo schema col <c>PostgresSchemaReconciler</c>: un
/// backfill scritto in SQL di migrazione non girerebbe in produzione.
/// </summary>
public interface IDocumentMaintenance
{
    /// <summary>Assegna una chiave univoca alle sezioni libere nate con la chiave storica ambigua
    /// <c>"custom"</c>. Ritorna il numero di sezioni riconciliate.</summary>
    Task<int> ReconcileCustomSectionKeysAsync(CancellationToken ct = default);
}
