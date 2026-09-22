using System.Drawing;

namespace Vipi.Sectorfile.Shared;

/// <summary>
/// Named-colour table built from a .def file. Lookups are case-insensitive.
/// Unknown names resolve to a magenta fallback (never throws) so that an
/// unrecognised palette reference is visible on the map rather than crashing a parser.
/// </summary>
public sealed class ColorPalette
{
    private readonly Dictionary<string, ColorDefinition> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, ColorDefinition> Entries => _entries;

    /// <summary>Adds or replaces a colour definition (last write wins).</summary>
    public void Add(ColorDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _entries[definition.Name] = definition;
    }

    /// <summary>Attempts to resolve a name without falling back.</summary>
    public bool TryResolve(string name, out ColorDefinition definition)
    {
        if (name is not null && _entries.TryGetValue(name, out var found))
        {
            definition = found;
            return true;
        }

        definition = default!;
        return false;
    }

    /// <summary>
    /// Resolves a colour name, falling back to magenta (with the requested name preserved)
    /// when the name is unknown. Never throws.
    /// </summary>
    public ColorDefinition Resolve(string name)
        => TryResolve(name, out var found)
            ? found
            : new ColorDefinition(name ?? string.Empty, Color.Magenta);
}
