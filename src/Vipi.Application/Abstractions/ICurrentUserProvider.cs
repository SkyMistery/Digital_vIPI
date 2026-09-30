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

    /// <summary>La divisione IVAO (IT, FR, …), dal profilo. Null dove l'host non la dà (sviluppo, cookie di prima
    /// del 30 settembre 2026). La legge il registro degli accessi.</summary>
    public string? Division { get; init; }
}

/// <summary>
/// Astrazione di portabilità: l'host fornisce l'adapter (HostIdentity per A/B, OIDC per C). ADR-0002 D2/D3.
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>Utente corrente, o null se anonimo.</summary>
    CurrentUser? Get();
}
