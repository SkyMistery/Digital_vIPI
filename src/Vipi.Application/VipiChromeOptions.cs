namespace Vipi.Application;

/// <summary>
/// Opzioni di "chrome" del modulo (sezione config "Vipi"). Quando il modulo è agganciato a un sito che ha
/// già la propria header/navigazione, l'host può disattivare la topbar del modulo
/// (<c>"Vipi": { "RenderTopbar": false }</c>) per evitare la doppia barra.
/// </summary>
public sealed class VipiChromeOptions
{
    public const string SectionName = "Vipi";

    /// <summary>Se mostrare la topbar propria del modulo. Default true (host standalone).</summary>
    public bool RenderTopbar { get; set; } = true;
}
