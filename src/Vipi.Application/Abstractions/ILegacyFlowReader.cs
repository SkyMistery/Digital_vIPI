using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Lettura dei **flussi storici** (`TransferFlows`/`TransferPoints`), che esistono ancora solo per una ragione:
/// il travaso agli accordi deve poterli leggere.
///
/// <para>Era <c>ITransferRepository</c>, e portava anche tutta la scrittura — venti operazioni fra outline,
/// varianti e ripristini. Quella è passata a <see cref="IAgreementRepository"/> insieme al modello; qui resta
/// solo il metodo che serve a <c>IAgreementMaintenance</c>. Il nome dice cosa è rimasto, perché un nome che
/// descrive un meccanismo sparito mente a chi legge fra sei mesi.</para>
///
/// <para>⚠️ Sparisce con la migrazione che droppa le due tabelle, e quella arriva <b>dopo</b> che il travaso è
/// stato eseguito e verificato in produzione: nella stessa release non ci starebbe, perché le migrazioni girano
/// prima della manutenzione d'avvio e il travaso non troverebbe più niente da leggere.</para>
/// </summary>
public interface ILegacyFlowReader
{
    /// <summary>I flussi storici di una ACC, coi loro punti. Vuoto se la ACC non esiste o non ne ha.</summary>
    Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default);
}
