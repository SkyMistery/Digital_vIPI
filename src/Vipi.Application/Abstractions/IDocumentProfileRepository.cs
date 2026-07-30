using Vipi.Application.Content;

namespace Vipi.Application.Abstractions;

/// <summary>
/// Override editoriali data-driven di un documento vIPI (side-entity 1:1 <c>DocumentProfile</c>): scelte dello staff
/// sulle sezioni DERIVATE (nascondi settori/frequenze, ordine e link frequenze). Le sezioni testuali
/// vivono nel Document; qui solo ciò che non è testo. Chiave = documentId. Doc refactor 08e.
/// </summary>
public interface IDocumentProfileRepository
{
    /// <summary>Legge gli override del documento (tutti vuoti se non esiste ancora una riga).</summary>
    Task<DocumentProfileData> GetAsync(int documentId, CancellationToken ct = default);

    /// <summary>Override d'ordine delle frequenze per callsign.</summary>
    Task SaveFreqOrderAsync(int documentId, IReadOnlyList<AppFreqOrderOverride> overrides, CancellationToken ct = default);

    /// <summary>Id dei settori sorgente dei link frequenza extra.</summary>
    Task SaveFreqLinksAsync(int documentId, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default);
}

/// <summary>Override letti dal <c>DocumentProfile</c> (già deserializzati). Liste vuote / null se assenti.</summary>
public sealed class DocumentProfileData
{
    public IReadOnlyList<AppFreqOrderOverride> FreqOrder { get; init; } = Array.Empty<AppFreqOrderOverride>();
    public IReadOnlyList<int> FreqLinkSectorIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<string> HiddenAorSectors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> HiddenFrequencies { get; init; } = Array.Empty<string>();
}
