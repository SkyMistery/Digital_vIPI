using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Storage del lock di editing esclusivo su risorse nominate (gemello per-risorsa del lock del Document su
/// <c>IEditingRepository</c>). Acquisizione atomica DB-side; rilascio = rimozione riga. Ritorna <see cref="LockInfo"/>.
/// </summary>
public interface IResourceLockRepository
{
    /// <summary>Acquisisce (o rinnova) il lock se libero/scaduto/già mio; altrimenti ispeziona il lock altrui. Atomico.</summary>
    Task<LockInfo> AcquireOrInspectAsync(string resourceKey, int userId, string? name, int ttlMinutes, CancellationToken ct = default);

    /// <summary>Stato corrente del lock (Free se assente o scaduto).</summary>
    Task<LockInfo> InspectAsync(string resourceKey, int userId, CancellationToken ct = default);

    /// <summary>Rinnova la scadenza (sliding) se il lock è del UserId indicato.</summary>
    Task RenewAsync(string resourceKey, int userId, int ttlMinutes, CancellationToken ct = default);

    /// <summary>True se il lock è tenuto dal UserId indicato e non scaduto.</summary>
    Task<bool> IsHeldByAsync(string resourceKey, int userId, CancellationToken ct = default);

    /// <summary>Rilascia il lock se è del UserId indicato.</summary>
    Task ReleaseAsync(string resourceKey, int userId, CancellationToken ct = default);

    /// <summary>Sblocca comunque (admin), a prescindere dal proprietario.</summary>
    /// <summary>Libera il lock chiunque lo tenga. <paramref name="actorUserId"/> è chi forza: finisce nel
    /// registro di audit insieme al nome di chi lo teneva.</summary>
    Task ForceUnlockAsync(string resourceKey, int actorUserId, CancellationToken ct = default);
}
