namespace Vipi.Application.Media;

/// <summary>
/// Parametri del caricamento immagini (sezione <c>Media</c> di appsettings; su Render bastano le env var
/// <c>Media__MaxUploadBytes</c> e simili, senza toccare il codice).
/// <para>
/// <see cref="MaxUploadBytes"/> è letto in un solo posto e usato in quattro: testo d'aiuto nella UI, limite dello
/// stream in ingresso, controllo lato server e messaggio di rifiuto. Cambiare il limite = cambiare questo numero.
/// </para>
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Dimensione massima del singolo file caricato, in byte. Default 3 MB.</summary>
    public int MaxUploadBytes { get; set; } = 3 * 1024 * 1024;

    /// <summary>Lato massimo in pixel accettato: guardia contro le immagini-bomba (dimensioni enormi, file piccolo).</summary>
    public int MaxImagePixels { get; set; } = 12000;

    /// <summary>
    /// Spazio massimo che le immagini di UN documento possono occupare, in byte. <c>0</c> = nessun limite.
    /// Default 25 MB: con foto gia' rimpicciolite dal browser sono decine di immagini per documento, ma impedisce
    /// che un solo documento si mangi il database condiviso.
    /// </summary>
    public int MaxBytesPerDocument { get; set; } = 25 * 1024 * 1024;

    /// <summary>Lato lungo a cui il browser rimpicciolisce l'immagine PRIMA di caricarla (0 = nessun ridimensionamento).</summary>
    public int ClientDownscaleLongestSidePx { get; set; } = 2000;

    /// <summary>Qualità di ricodifica usata dal ridimensionamento nel browser (0..1).</summary>
    public double JpegQuality { get; set; } = 0.85;

    /// <summary>Il limite come lo legge un umano ("3 MB"): un solo posto anche per il testo, così UI e messaggi
    /// d'errore non possono dire due numeri diversi.</summary>
    public string MaxUploadLabel => Human(MaxUploadBytes);

    /// <summary>La quota per documento come la legge un umano.</summary>
    public string MaxBytesPerDocumentLabel => Human(MaxBytesPerDocument);

    internal static string Human(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / (double)(1024 * 1024):0.#} MB",
        >= 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes} byte",
    };
}
