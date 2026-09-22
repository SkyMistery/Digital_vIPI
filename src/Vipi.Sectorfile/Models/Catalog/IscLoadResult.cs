using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>
/// Output of Phase 0 (<c>IscLoader</c>): the parsed <see cref="Info"/>, the colour <see cref="Palette"/>
/// loaded from <c>[DEFINE]</c>, the built <see cref="Catalog"/>, and the per-section buckets of resolved
/// absolute file paths (consumed by the Phase 1/2 loaders). Section names are stored uppercased.
/// </summary>
public sealed class IscLoadResult
{
    public IscInfo Info { get; set; } = new();
    public ColorPalette Palette { get; set; } = new();
    public Catalog Catalog { get; set; } = new();

    /// <summary>Section name (uppercased) → resolved absolute file paths, in file order, duplicates accumulated.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Sections { get; set; }
        = new Dictionary<string, IReadOnlyList<string>>();
}
