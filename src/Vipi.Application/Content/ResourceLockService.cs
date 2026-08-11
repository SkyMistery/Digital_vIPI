using Vipi.Application.Abstractions;
using Vipi.Application.Auth;

namespace Vipi.Application.Content;

/// <summary>Chiavi delle risorse con lock di editing esclusivo (pagine senza documento sottostante).</summary>
public static class ResourceLockKeys
{
    /// <summary>Struttura del catalogo (settori/ACC/aeroporti/trasferimenti): una persona edita la struttura per volta.</summary>
    public const string Structure = "admin:structure";

    /// <summary>Wizard di creazione nuovo documento.</summary>
    public const string NewDoc = "editor:newdoc";

    /// <summary>
    /// Chiavi che solo un admin può prendere. Il requisito è dichiarato qui e non dedotto dal prefisso
    /// «admin:» del nome, che è una convenzione e non un controllo.
    ///
    /// <para><b>Perché serve.</b> Fino all'11 agosto 2026 <see cref="IResourceLockService.AcquireAsync"/>
    /// chiedeva una cosa sola: che l'utente fosse autenticato. Le quattro pagine di struttura sono pagine
    /// admin, ma il loro lock lo poteva prendere qualunque membro IVAO loggato — e tenerlo per sempre, con
    /// l'heartbeat della UI. Gli admin restavano fuori dal proprio strumento finché l'altro non smetteva.
    /// Non era un blocco (il force-unlock è già riservato agli admin) ma un fastidio ripetibile a piacere.</para>
    ///
    /// <para><c>editor:newdoc</c> resta fuori di proposito: creare un documento è già filtrato dai grant
    /// per ACC dentro il servizio che lo crea, e chiedere l'admin qui toglierebbe il lock a chi ha il
    /// permesso di scrivere.</para>
    /// </summary>
    public static readonly IReadOnlySet<string> RichiedonoAdmin =
        new HashSet<string>(StringComparer.Ordinal) { Structure };
}

/// <summary>
/// Lock di editing esclusivo su risorse nominate (gemello per-risorsa del lock del Document in <see cref="IEditingService"/>).
/// Owner = utente corrente (<see cref="IEditAuthorizationService"/>); TTL corto rinnovato via heartbeat dalla UI, così la
/// chiusura della scheda libera da sé in pochi minuti. Il rilascio esplicito e il force-unlock (admin) sono immediati.
/// </summary>
public interface IResourceLockService
{
    /// <summary>Prova ad acquisire/rinnovare il lock per l'utente corrente. Free se non autenticato.</summary>
    Task<LockInfo> AcquireAsync(string resourceKey, CancellationToken ct = default);

    /// <summary>Stato corrente del lock (senza acquisirlo).</summary>
    Task<LockInfo> InspectAsync(string resourceKey, CancellationToken ct = default);

    /// <summary>Heartbeat: rinnova la scadenza se il lock è mio, e ritorna lo stato aggiornato.</summary>
    Task<LockInfo> HeartbeatAsync(string resourceKey, CancellationToken ct = default);

    /// <summary>Rilascia il lock se è mio (bottone «Fine modifica» / teardown della UI).</summary>
    Task ReleaseAsync(string resourceKey, CancellationToken ct = default);

    /// <summary>Sblocca comunque (solo admin).</summary>
    Task ForceUnlockAsync(string resourceKey, CancellationToken ct = default);

    /// <summary>Garantisce che l'utente corrente tenga il lock (per gestire le azioni); rinnova la scadenza. Lancia <see cref="EditConflictException"/> altrimenti.</summary>
    Task EnsureHeldAsync(string resourceKey, CancellationToken ct = default);
}

/// <inheritdoc cref="IResourceLockService"/>
public sealed class ResourceLockService : IResourceLockService
{
    /// <summary>TTL corto: la UI rinnova ogni ~60s finché aperta; chiusa la scheda l'heartbeat si ferma e il lock scade.</summary>
    public const int LockTtlMinutes = 3;

    private readonly IResourceLockRepository _repo;
    private readonly IEditAuthorizationService _authz;

    public ResourceLockService(IResourceLockRepository repo, IEditAuthorizationService authz)
    {
        _repo = repo;
        _authz = authz;
    }

    public Task<LockInfo> AcquireAsync(string resourceKey, CancellationToken ct = default)
    {
        if (_authz.CurrentUserId is not int uid) return Task.FromResult(LockInfo.Free());
        // Le chiavi admin le prende solo un admin: vedi ResourceLockKeys.RichiedonoAdmin per il perché.
        if (ResourceLockKeys.RichiedonoAdmin.Contains(resourceKey)) _authz.EnsureAdmin();
        return _repo.AcquireOrInspectAsync(resourceKey, uid, _authz.CurrentName, LockTtlMinutes, ct);
    }

    public Task<LockInfo> InspectAsync(string resourceKey, CancellationToken ct = default) =>
        _repo.InspectAsync(resourceKey, _authz.CurrentUserId ?? 0, ct);

    public async Task<LockInfo> HeartbeatAsync(string resourceKey, CancellationToken ct = default)
    {
        if (_authz.CurrentUserId is int uid) await _repo.RenewAsync(resourceKey, uid, LockTtlMinutes, ct);
        return await _repo.InspectAsync(resourceKey, _authz.CurrentUserId ?? 0, ct);
    }

    public Task ReleaseAsync(string resourceKey, CancellationToken ct = default) =>
        _repo.ReleaseAsync(resourceKey, _authz.CurrentUserId ?? 0, ct);

    public Task ForceUnlockAsync(string resourceKey, CancellationToken ct = default)
    {
        if (!_authz.IsAdmin) throw new EditNotAllowedException();
        return _repo.ForceUnlockAsync(resourceKey, ct);
    }

    public async Task EnsureHeldAsync(string resourceKey, CancellationToken ct = default)
    {
        var uid = _authz.CurrentUserId ?? 0;
        if (!await _repo.IsHeldByAsync(resourceKey, uid, ct))
            throw new EditConflictException("Lock scaduto o preso da un altro editor: clicca «Inizia modifica» per riacquisirlo.");
        await _repo.RenewAsync(resourceKey, uid, LockTtlMinutes, ct);
    }
}
