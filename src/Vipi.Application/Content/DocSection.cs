namespace Vipi.Application.Content;

/// <summary>
/// Sezione di un documento nel modello unificato (doc refactor 08a). Ricorsiva: una sezione editoriale contiene
/// blocchi (<see cref="Blocks"/>) e sotto-sezioni (<see cref="SubSections"/>) — che sono a loro volta sezioni.
/// <see cref="Key"/> = chiave del catalogo per le sezioni fisse (null per le custom). Per le sezioni
/// <see cref="SectionKind.Derived"/> il contenuto è calcolato live dal renderer per key (Blocks/SubSections vuote).
/// </summary>
public sealed record DocSection(
    string Title,
    SectionKind Kind,
    string? Key = null,
    IReadOnlyList<DocBlock>? Blocks = null,
    IReadOnlyList<DocSection>? SubSections = null)
{
    public IReadOnlyList<DocBlock> Blocks { get; init; } = Blocks ?? Array.Empty<DocBlock>();
    public IReadOnlyList<DocSection> SubSections { get; init; } = SubSections ?? Array.Empty<DocSection>();
}
