namespace Vipi.Application.Content;

/// <summary>Riferimento per hide/delete: coincide con (Kind, ReleaseTarget, ReleaseKey, DocumentId) di un ManagedDoc.</summary>
public sealed record ManagedDocRef(ManagedDocKind Kind, string ReleaseKey, int? DocumentId);
