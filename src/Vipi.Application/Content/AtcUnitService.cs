using System.Text.RegularExpressions;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using static Vipi.Application.Messaggio;

namespace Vipi.Application.Content;

/// <summary>
/// Le posizioni di un ente ATC, come le cambia lo staff (S49, committente, 29 settembre 2026): a Pratica di Mare
/// l'ente passa da <c>LIRE_APP</c> a <c>LIRE_TWR</c> senza toccare il documento, né le sue pubblicazioni.
/// </summary>
public interface IAtcUnitService
{
    /// <summary>L'ente per codice o per una sua posizione; null se nessuno.</summary>
    Task<AtcUnitRow?> FindAsync(string key, CancellationToken ct = default);

    Task AddPositionAsync(int unitId, string callsign, CancellationToken ct = default);
    Task RemovePositionAsync(int unitId, string callsign, CancellationToken ct = default);
    Task MakePrimaryAsync(int unitId, string callsign, CancellationToken ct = default);
    Task RenameAsync(int unitId, string name, CancellationToken ct = default);
}

/// <inheritdoc cref="IAtcUnitService"/>
public sealed partial class AtcUnitService : IAtcUnitService
{
    private readonly IAtcUnitRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public AtcUnitService(IAtcUnitRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    /// <summary>Un nominativo IVAO: lettere, cifre e trattini bassi (<c>LIRE_TWR</c>, <c>LIPE_W_APP</c>).</summary>
    [GeneratedRegex(@"^[A-Z0-9]+(_[A-Z0-9]+)+$")]
    private static partial Regex Nominativo();

    public Task<AtcUnitRow?> FindAsync(string key, CancellationToken ct = default) => _repo.FindAsync(key, ct);

    public Task AddPositionAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var cs = (callsign ?? "").Trim().ToUpperInvariant();
        if (!Nominativo().IsMatch(cs))
            throw new Aor.ValidationException(Lingua($"«{cs}» non è un nominativo (es. LIRE_TWR).",
                $"«{cs}» is not a callsign (e.g. LIRE_TWR)."));
        return _repo.AddPositionAsync(unitId, cs, ct);
    }

    public Task RemovePositionAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        return _repo.RemovePositionAsync(unitId, callsign, ct);
    }

    public Task MakePrimaryAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        return _repo.MakePrimaryAsync(unitId, callsign, ct);
    }

    public Task RenameAsync(int unitId, string name, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        return _repo.RenameAsync(unitId, name, ct);
    }
}
