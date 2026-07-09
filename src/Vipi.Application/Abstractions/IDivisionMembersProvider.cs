namespace Vipi.Application.Abstractions;

/// <summary>Membro (controller) della divisione, per l'auto-elenco nella schermata permessi. Fase G.</summary>
public sealed record DivisionMember(int UserId, string Name, string Rating);

/// <summary>
/// Porta verso l'elenco controller della divisione IVAO Italia. Usata per popolare il dropdown
/// in /vsop/admin/permessi (oggi solo UserId manuale). Richiede credenziali API. Impl. = IvaoDivisionClient.
/// </summary>
public interface IDivisionMembersProvider
{
    Task<IReadOnlyList<DivisionMember>> GetDivisionControllersAsync(CancellationToken ct = default);
}
