using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>Persistenza dei settori orfani (proiettati e non più confermati dai cataloghi). Impl. EF.</summary>
public interface IOrphanSectorRepository
{
    /// <summary>Gli orfani, con i documenti che li raccontano e chi ne impedisce la rimozione.</summary>
    Task<IReadOnlyList<OrphanSectorRow>> ListOrphansAsync(string? accCode, CancellationToken ct = default);

    /// <summary>Un singolo orfano (per i controlli prima di rimuoverlo). null se non è orfano.</summary>
    Task<OrphanSectorRow?> GetOrphanAsync(int sectorId, CancellationToken ct = default);

    /// <summary>Settori attivi dello stesso ACC a cui si può riappendere il documento dell'orfano.</summary>
    Task<IReadOnlyList<ReattachTargetRow>> ReattachTargetsAsync(int orphanSectorId, CancellationToken ct = default);

    /// <summary>Sposta documento e ruolo di primario dall'orfano al bersaglio.</summary>
    Task ReattachAsync(int orphanSectorId, int targetSectorId, CancellationToken ct = default);

    /// <summary>Cancella la riga proiettata e quella di catalogo, se c'è ancora.</summary>
    Task RemoveAsync(int orphanSectorId, CancellationToken ct = default);

    /// <summary>Codice ACC del settore, per l'autorizzazione. null se non risolvibile.</summary>
    Task<string?> GetAccCodeAsync(int sectorId, CancellationToken ct = default);
}
