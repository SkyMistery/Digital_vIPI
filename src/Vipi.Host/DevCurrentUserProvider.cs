using Vipi.Application.Abstractions;

namespace Vipi.Host;

/// <summary>
/// Adapter di sviluppo per <see cref="ICurrentUserProvider"/>: utente CH fittizio per provare il percorso editing.
/// In produzione è sostituito da HostIdentityCurrentUserProvider (scenari A/B, legge il ClaimsPrincipal)
/// o OidcCurrentUserProvider (scenario C). ADR-0002 D2/D3.
/// </summary>
public sealed class DevCurrentUserProvider : ICurrentUserProvider
{
    public CurrentUser? Get() => new(
        Vid: 654321,
        Name: "Dev User",
        Fir: "LIRR",
        StaffPositions: new[] { "IT-WM", "IT-AOC" })
    {
        CanEdit = true,
    };
}
