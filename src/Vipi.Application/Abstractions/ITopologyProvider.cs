using Vipi.Application.Aor;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Porta che fornisce la <see cref="Topology"/> pura a partire dall'anagrafica persistita.
/// Tiene la UI/Application indipendenti da EF/Infrastructure (impl. = TopologyBuilder).
/// </summary>
public interface ITopologyProvider
{
    /// <summary>Costruisce la topologia di una FIR dato il suo codice (es. "LIRR"). Null se la FIR non esiste.</summary>
    Task<Topology?> BuildByFirCodeAsync(string firCode, CancellationToken ct = default);
}
