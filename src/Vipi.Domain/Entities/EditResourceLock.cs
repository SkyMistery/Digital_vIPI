namespace Vipi.Domain.Entities;

/// <summary>
/// Lock di editing esclusivo su una RISORSA con-chiave (non un <c>Document</c>): pagine admin di struttura (catalogo
/// ACC/settori/aeroporti/trasferimenti) e wizard nuovo documento. Una riga per <see cref="ResourceKey"/> mentre il lock
/// è tenuto; rilasciandolo la riga viene rimossa. Semantica gemella del lock del Document (owner VID+nome, TTL sliding),
/// ma per risorse nominate invece che per documentId. Vedi <c>ResourceLockKeys</c>.
/// </summary>
public class EditResourceLock
{
    public int Id { get; set; }

    /// <summary>Chiave della risorsa bloccata (es. <c>admin:structure</c>, <c>editor:newdoc</c>). Univoca.</summary>
    public string ResourceKey { get; set; } = "";

    public int LockedByUserId { get; set; }
    public string? LockedByName { get; set; }
    public DateTime LockedAtUtc { get; set; }
    public DateTime LockExpiresUtc { get; set; }
}
