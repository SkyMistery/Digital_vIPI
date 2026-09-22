using System.Drawing;

namespace Vipi.Sectorfile.Shared;

/// <summary>
/// A single named colour from the .def palette.
/// Uses <see cref="System.Drawing.Color"/> (no WPF/SkiaSharp dependency) — converted to
/// the render colour type at the ViewModel layer only, keeping Shared BCL-only.
/// </summary>
/// <param name="Name">Palette name, e.g. "TAXIWAY".</param>
/// <param name="Value">Resolved colour.</param>
public sealed record ColorDefinition(string Name, Color Value);
