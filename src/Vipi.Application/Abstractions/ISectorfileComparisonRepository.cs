using Vipi.Application.Diagnostics;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Carica il lato <b>vIPI</b> del confronto col sectorfile: posizioni, aeroporti, piste, più i codici delle
/// ACC (che nella tabella degli aeroporti ci sono e aeroporti non sono).
/// <para>Sola lettura, quattro query, nessuna scrittura: questo giro non importa niente.</para>
/// </summary>
public interface ISectorfileComparisonRepository
{
    Task<SectorfileComparisonDataset> LoadAsync(CancellationToken ct = default);
}
