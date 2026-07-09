using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Tipo di documento gestibile nell'elenco unificato (Bozze &amp; versioni).</summary>
public enum ManagedDocKind { Vloa, AirportVipi, AccVipi, AppVipi }

/// <summary>
/// Documento gestibile nell'elenco unificato di /vsop/versioni: unifica i <see cref="Domain.Entities.Document"/>
/// (vLOA + vIPI aeroporto) e i profili (vIPI ACC = AccProfile, APP standalone = AppProfile). Porta identità +
/// stato (pubblicato/bozza/nascosto) + la chiave di release per la timeline. Versioni/release si caricano a parte
/// (lazy) all'espansione della riga.
/// </summary>
public sealed record ManagedDoc(
    ManagedDocKind Kind,
    string Title,
    string Scope,
    string? AccCode,
    bool IsPublished,
    bool HasDraft,
    bool IsHidden,
    ReleaseTargetType ReleaseTarget,
    string ReleaseKey,
    int? DocumentId,
    string? NeighbourCode = null);

/// <summary>Riferimento per hide/delete: coincide con (Kind, ReleaseTarget, ReleaseKey, DocumentId) di un ManagedDoc.</summary>
public sealed record ManagedDocRef(ManagedDocKind Kind, string ReleaseKey, int? DocumentId);

/// <summary>
/// Stato release riassuntivo di un bersaglio, per mostrarlo sulla riga collassata di /vsop/versioni senza espanderla.
/// Caricato in blocco per tutte le righe. <see cref="EffectiveCycle"/> = ciclo della release in vigore ora (null =
/// il pubblico vede il fallback live); <see cref="NextScheduledCycle"/> = ciclo della prossima release futura, se c'è.
/// </summary>
public sealed record ReleaseSummary(string? EffectiveCycle, string? NextScheduledCycle, bool HasAnyRelease);
