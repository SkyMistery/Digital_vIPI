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

    /// <summary>
    /// Versione in forma corta (es. <c>g · 17a6060</c>), mostrata <b>solo allo staff</b> nel piè di pagina
    /// (dal 29 settembre 2026; prima era in barra, ai soli admin). Vuota ⇒ non si mostra niente: il modulo non
    /// deve inventarsi un numero quando l'host non gliene passa uno.
    /// </summary>
    public string? Versione { get; set; }

    /// <summary>La stessa cosa per esteso, per il passaggio del mouse: pacchetto, commit, e da quando è in servizio.</summary>
    public string? VersioneDettaglio { get; set; }
}
