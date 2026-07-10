using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Documento gestibile nell'elenco unificato di /vsop/versioni. Post doc 08 tutti e 4 i tipi (vLOA, vIPI aeroporto,
/// vIPI ACC, APP standalone) sono su <see cref="Domain.Entities.Document"/>: questo record ne porta identità +
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
