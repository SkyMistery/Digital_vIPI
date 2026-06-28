namespace Vipi.Application.Abstractions;

/// <summary>
/// Modello utente neutro, indipendente dall'host. ADR-0002 D2/D5.
/// La logica non conosce ClaimsPrincipal/Identity/OIDC: chiede sempre qui "chi è l'utente?".
/// </summary>
public sealed record CurrentUser(
    int UserId,
    string Name,
    string? Acc,
    IReadOnlyCollection<string> StaffPositions)
{
    /// <summary>Vero se l'utente è CH/AOD della divisione IT → abilitato all'editing (RF-7).</summary>
    public bool CanEdit { get; init; }
}

/// <summary>
/// Astrazione di portabilità: l'host fornisce l'adapter (HostIdentity per A/B, OIDC per C). ADR-0002 D2/D3.
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>Utente corrente, o null se anonimo.</summary>
    CurrentUser? Get();
}
