using Vipi.Application.Aor;
using static Vipi.Application.Messaggio;

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
            throw new ValidationException(Lingua("Il file è vuoto.", "The file is empty."));

        if (bytes.Length > options.MaxUploadBytes)
            throw new ValidationException(TooBigMessage(bytes.Length, options));

        var info = ImageProbe.Inspect(bytes.Span)
            ?? throw new ValidationException(Lingua("Il file non è un'immagine in un formato supportato (PNG, JPEG, WebP, GIF).", "The file is not an image in a supported format (PNG, JPEG, WebP, GIF)."));

        if (info.Width <= 0 || info.Height <= 0)
            throw new ValidationException(Lingua("L'immagine dichiara dimensioni non valide.", "The image declares invalid dimensions."));

        if (info.Width > options.MaxImagePixels || info.Height > options.MaxImagePixels)
            throw new ValidationException(Lingua(
                $"L'immagine è {info.Width}×{info.Height} pixel: il massimo per lato è {options.MaxImagePixels}.",
                $"The image is {info.Width}×{info.Height} pixels: the maximum per side is {options.MaxImagePixels}."));

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
    /// <summary>
    /// Quota del documento esaurita. Dice quanto pesa gia' e quanto e' il tetto: senza i due numeri chi scrive non
    /// sa se deve togliere un'immagine o dieci.
    /// </summary>
    public static string QuotaMessage(long usati, MediaOptions options) =>
        $"Le immagini di questo documento occupano gia' {MediaOptions.Human(usati)} sui {options.MaxBytesPerDocumentLabel} disponibili: " +
        "rimuovi un'immagine che non serve piu' prima di aggiungerne altre.";

    public static string TooBigMessage(MediaOptions options) =>
        $"L'immagine supera il limite di {options.MaxUploadLabel}.";
}
