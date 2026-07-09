namespace Vipi.Application.Content;

/// <summary>
/// Porta di sola LETTURA sui candidati confinanti (ACC esteri geometricamente adiacenti). Serve ai consumatori
/// che devono solo leggere l'elenco senza poter triggerare l'import/generazione vLOA (es. l'editor trasferimenti,
/// per i mittenti estero→home). ISP: separa la lettura dal service import completo (doc refactor 07 §4.2).
/// </summary>
public interface INeighbourReader
{
    /// <summary>Elenco dei candidati confinanti (coppie Home↔Foreign) con stato ed eventuale vLOA.</summary>
    Task<IReadOnlyList<NeighbourCandidateRow>> ListAsync(CancellationToken ct = default);
}
