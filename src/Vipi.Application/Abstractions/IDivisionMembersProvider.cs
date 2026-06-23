namespace Vipi.Application.Abstractions;

/// <summary>Membro (controller) della divisione, per l'auto-elenco nella schermata permessi. Fase G.</summary>
public sealed record DivisionMember(int Vid, string Name, string Rating);

/// <summary>
/// Porta verso l'elenco controller della divisione IVAO Italia. Usata per popolare il dropdown
/// in /sop/admin/permessi (oggi solo VID manuale). Richiede credenziali API. Impl. = IvaoApiClient.
/// </summary>
public interface IDivisionMembersProvider
{
    Task<IReadOnlyList<DivisionMember>> GetDivisionControllersAsync(CancellationToken ct = default);
}
