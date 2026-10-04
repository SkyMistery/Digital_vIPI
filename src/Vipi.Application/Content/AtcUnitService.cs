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
    private readonly IDocumentLockGuard _lock;

    public AtcUnitService(IAtcUnitRepository repo, IEditAuthorizationService authz, IDocumentLockGuard lockGuard)
    {
        _repo = repo;
        _authz = authz;
        _lock = lockGuard;
    }

    /// <summary>
    /// Ruolo e LOCK della vIPI APP dell'ente (revisione, S52): le posizioni decidono frequenze, AoR e coordinamenti
    /// del documento, quindi cambiarle è scriverlo. Prima bastava il ruolo, e da una seconda scheda — o col lock
    /// scaduto e preso da un altro — si cambiava il documento sotto chi lo stava scrivendo.
    /// </summary>
    private async Task EnsureWritableAsync(int unitId, CancellationToken ct)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var ente = await _repo.GetAsync(unitId, ct)
                   ?? throw new Aor.ValidationException(Lingua($"Ente {unitId} inesistente.", $"Unit {unitId} does not exist."));
        if (ente.DocumentId is int doc) await _lock.EnsureMineAsync(doc, ct);
    }

    /// <summary>Un nominativo IVAO: lettere, cifre e trattini bassi (<c>LIRE_TWR</c>, <c>LIPE_W_APP</c>).</summary>
    [GeneratedRegex(@"^[A-Z0-9]+(_[A-Z0-9]+)+$")]
    private static partial Regex Nominativo();

    public Task<AtcUnitRow?> FindAsync(string key, CancellationToken ct = default) => _repo.FindAsync(key, ct);

    public async Task AddPositionAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var cs = (callsign ?? "").Trim().ToUpperInvariant();
        if (!Nominativo().IsMatch(cs))
            throw new Aor.ValidationException(Lingua($"«{cs}» non è un nominativo (es. LIRE_TWR).",
                $"«{cs}» is not a callsign (e.g. LIRE_TWR)."));
        await EnsureWritableAsync(unitId, ct);
        await _repo.AddPositionAsync(unitId, cs, ct);
    }

    public async Task RemovePositionAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        await EnsureWritableAsync(unitId, ct);
        await _repo.RemovePositionAsync(unitId, callsign, ct);
    }

    public async Task MakePrimaryAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        await EnsureWritableAsync(unitId, ct);
        await _repo.MakePrimaryAsync(unitId, callsign, ct);
    }

    public async Task RenameAsync(int unitId, string name, CancellationToken ct = default)
    {
        await EnsureWritableAsync(unitId, ct);
        await _repo.RenameAsync(unitId, name, ct);
    }
}
