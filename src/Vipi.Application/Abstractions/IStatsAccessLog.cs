namespace Vipi.Application.Abstractions;

/// <summary>
/// Registra nell'audit l'apertura, da parte dello staff, delle statistiche personali di un <b>altro</b>
/// controllore.
///
/// <para><b>Perché una lettura finisce nel registro.</b> Tutto il resto dell'audit descrive atti che
/// cambiano qualcosa; questa no. Ma la pagina personale non è la classifica: porta la griglia ora×giorno —
/// cioè <i>quando</i> quella persona è di solito online — l'elenco dei turni e le postazioni preferite. È
/// un dato sulla persona, non sulla divisione, e chi lo guarda deve poterlo spiegare. Non si registra
/// nessun'altra lettura: il registro di audit non è un log di navigazione.</para>
/// </summary>
public interface IStatsAccessLog
{
    /// <summary>
    /// Segna che <paramref name="actorUserId"/> ha aperto le statistiche di <paramref name="subjectUserId"/>.
    ///
    /// <para>⚠️ L'implementazione <b>accorpa</b> gli accessi ravvicinati alla stessa persona: la pagina si
    /// ricarica a ogni chip di periodo e a ogni F5, e una riga per ricarica trasformerebbe una consultazione
    /// in venti eventi — il registro diventa illeggibile proprio dove serve leggerlo. Chiamarla a ogni
    /// apertura è quindi corretto e voluto.</para>
    /// </summary>
    Task RecordProfileViewAsync(int actorUserId, int subjectUserId, CancellationToken ct = default);
}
