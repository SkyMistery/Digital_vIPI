using Vipi.Application.Aor;

namespace Vipi.Application.Media;

/// <summary>
/// Regole di accettazione di un'immagine caricata. Vive qui, e non nell'adapter di persistenza, perché sono una
/// decisione di dominio applicativo: valgono qualunque sia il posto in cui finiscono i byte (oggi il DB, domani
/// magari un object storage). Solleva <see cref="ValidationException"/> — mai DataAnnotations: la UI cattura questa,
/// e un'eccezione non catturata in un circuito Blazor è una pagina bianca.
/// </summary>
public static class MediaValidator
{
    /// <summary>Controlla limite di dimensione, formato reale e pixel; restituisce l'immagine riconosciuta.</summary>
    public static ImageInfo Validate(ReadOnlyMemory<byte> bytes, MediaOptions options)
    {
        if (bytes.Length == 0)
            throw new ValidationException("Il file è vuoto.");

        if (bytes.Length > options.MaxUploadBytes)
            throw new ValidationException(TooBigMessage(bytes.Length, options));

        var info = ImageProbe.Inspect(bytes.Span)
            ?? throw new ValidationException("Il file non è un'immagine in un formato supportato (PNG, JPEG, WebP, GIF).");

        if (info.Width <= 0 || info.Height <= 0)
            throw new ValidationException("L'immagine dichiara dimensioni non valide.");

        if (info.Width > options.MaxImagePixels || info.Height > options.MaxImagePixels)
            throw new ValidationException(
                $"L'immagine è {info.Width}×{info.Height} pixel: il massimo per lato è {options.MaxImagePixels}.");

        return info;
    }

    /// <summary>Il messaggio di «troppo grande» quando la dimensione reale è nota.</summary>
    public static string TooBigMessage(long bytes, MediaOptions options) =>
        $"L'immagine pesa {MediaOptions.Human(bytes)}: il limite è {options.MaxUploadLabel}.";

    /// <summary>
    /// Variante senza la dimensione, per quando il limite scatta sul TRASPORTO: lo stream si interrompe prima che
    /// il deposito veda i byte, e quel che si stava inviando può essere la copia già rimpicciolita dal browser —
    /// citare la dimensione del file scelto direbbe un numero che non è quello rifiutato.
    /// </summary>
    public static string TooBigMessage(MediaOptions options) =>
        $"L'immagine supera il limite di {options.MaxUploadLabel}.";
}
