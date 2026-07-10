namespace Vipi.Application.Content;

/// <summary>Overlay di visibilità congelato nella release (fotografia di <c>DocumentProfile</c>).</summary>
public sealed class VloaOverlaySnapshot
{
    public List<string> HiddenAorSectors { get; set; } = new();
    public List<string> HiddenFrequencies { get; set; } = new();
    public List<string> HiddenSections { get; set; } = new();
}
