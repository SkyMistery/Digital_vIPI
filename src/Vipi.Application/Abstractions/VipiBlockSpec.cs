namespace Vipi.Application.Abstractions;

/// <summary>
/// Spec di un blocco per la creazione annidata della vIPI ACC (<see cref="IEditingRepository.EnsureVipiDocumentTreeAsync"/>):
/// una sezione radice (depth 0) con chiave <paramref name="Key"/> e titolo <paramref name="Title"/>, e le sue
/// sezioni-catalogo (depth 1) in <paramref name="Sections"/> (chiave + titolo, nell'ordine dato). Doc refactor 08e-acc.
/// </summary>
public sealed record VipiBlockSpec(string Key, string Title, IReadOnlyList<(string Key, string Title)> Sections);
