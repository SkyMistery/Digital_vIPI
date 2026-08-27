using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Documento gestibile nell'elenco unificato di /services/vsop/versioni. Post doc 08 tutti e 4 i tipi (vLOA, vIPI aeroporto,
/// vIPI ACC, APP standalone) sono su <see cref="Domain.Entities.Document"/>: questo record ne porta identità +
/// stato (pubblicato/bozza/nascosto) + la chiave di release per la timeline. Versioni/release si caricano a parte
/// (lazy) all'espansione della riga.
/// </summary>
public sealed record ManagedDoc(
    ReleaseTargetType Kind,
    string Title,
    string Scope,
    string? AccCode,
    bool IsPublished,
    bool HasDraft,
    bool IsHidden,
    ReleaseTargetType ReleaseTarget,
    string ReleaseKey,
    int? DocumentId,
    string? NeighbourCode = null,
    /// <summary>Ciclo AIRAC della release EFFETTIVA adesso, se c'è. Popolato da <c>IDocumentAdminRepository.ListAsync</c>
    /// con la stessa query batch che serviva il solo <see cref="HasEffectiveRelease"/>; il descrittore per-tipo lo
    /// lascia null (non conosce le release).</summary>
    string? EffectiveCycle = null,
    /// <summary>Ciclo AIRAC della prossima release programmata, se c'è. Stessa provenienza di <see cref="EffectiveCycle"/>.</summary>
    string? NextScheduledCycle = null,
    /// <summary>Il bersaglio ha almeno una release non superata. ⚠️ Non è <c>EffectiveCycle is not null</c>: un
    /// documento può avere solo release <b>future</b> — «senza release» e «programmata ma non ancora in vigore»
    /// sono due stati diversi, e l'elenco li distingue.</summary>
    bool HasAnyRelease = false,
    /// <summary>VID di chi tiene il lock di editing ATTIVO (null = libero o scaduto). Viene dal <c>Document</c>
    /// letto dalla stessa query dell'elenco: nessuna interrogazione in più, nessun N+1.</summary>
    int? LockedByUserId = null,
    /// <summary>Nome di chi tiene il lock attivo, come lo aveva al momento dell'acquisizione.</summary>
    string? LockedByName = null,
    /// <summary>Scadenza del lock attivo (UTC). ⚠️ Il TTL del lock documento è 30 minuti e <b>non</b> ha heartbeat
    /// (a differenza di <c>EditResourceLock</c>): si rinnova al salvataggio e si libera con «Fine modifica», quindi
    /// una scheda chiusa lascia il lock in piedi fin quasi a mezz'ora. Per questo la UI mostra l'ora e offre il
    /// force-unlock agli admin.</summary>
    DateTime? LockExpiresUtc = null)
{
    /// <summary>Ha una release AIRAC EFFETTIVA adesso (doc 10 §3f): gate della visibilità pubblica.
    /// <para>È <b>calcolato</b>, non un campo suo: fino al 21 agosto 2026 il bool e il riepilogo release erano due
    /// dati per lo stesso fatto — il repo popolava il primo e /services/vsop/versions ricaricava il secondo con una seconda
    /// <c>SummariesAsync</c> identica.</para></summary>
    public bool HasEffectiveRelease => EffectiveCycle is not null;

    /// <summary>C'è un lock di editing attivo su questo documento (di chiunque).</summary>
    public bool IsLocked => LockedByUserId is not null;
}
