using System.Text.Json;

namespace Vipi.Application.Content;

/// <summary>
/// Come un blocco <c>Image</c> cita la sua immagine: <c>BodyJson</c> porta lo sha e i metadati, <c>Body</c> la
/// didascalia (markdown, come la prosa). Nel blocco non finiscono mai i byte — solo lo sha — così il payload di una
/// release resta leggero e l'immagine si serve dalla sua rotta con cache lunga.
/// <para>
/// Questa classe è la FONTE UNICA del formato: la usano i due editor, il viewer, il rebuild dell'aeroporto e
/// l'indice di ricerca. Se il formato cambia, cambia qui e basta.
/// </para>
/// </summary>
/// <param name="MediaId">Sha256 dei byte: e' l'identita' dell'immagine e la sua rotta pubblica.</param>
/// <param name="Alt">Testo alternativo per chi non vede l'immagine.</param>
/// <param name="Width">Larghezza NATIVA in pixel (la dice l'<c>ImageProbe</c>): riserva il posto prima che l'immagine arrivi.</param>
/// <param name="Height">Altezza nativa in pixel, stesso mestiere.</param>
/// <param name="Scale">
/// Larghezza di RESA in percentuale della colonna, 0 = piena (com'era prima che questo campo esistesse, quindi ogni
/// release gia' congelata continua a rendersi identica). Si conserva una percentuale e non dei pixel perche' la
/// stessa immagine si legge su un monitor, su un telefono e su un A4: solo un rapporto vale in tutti e tre.
/// </param>
public sealed record MediaRef(string MediaId, string? Alt = null, int Width = 0, int Height = 0, int Scale = 0)
{
    /// <summary>Sotto questa quota l'immagine non e' piu' guardabile: la maniglia si ferma qui.</summary>
    public const int MinScale = 10;

    /// <summary>Piena larghezza della colonna. Oltre non si va: ingrandire un raster lo sgrana.</summary>
    public const int MaxScale = 100;

    /// <summary>Riporta una percentuale dentro i limiti; 0 (o 100) = piena larghezza, cioe' nessuno stile da scrivere.</summary>
    public static int ClampScale(int scale) =>
        scale <= 0 || scale >= MaxScale ? 0 : Math.Max(MinScale, scale);

    /// <summary>Percentuale da mostrare all'editore: la piena larghezza si legge <c>100</c>, non <c>0</c>.</summary>
    public int ScaleOrFull => Scale > 0 ? Scale : MaxScale;

    /// <summary>Prefisso della rotta pubblica che serve i byte (vedi <c>MapVipiModule</c>).</summary>
    public const string UrlPrefix = "/vsop/media/";

    /// <summary>URL da mettere nel <c>src</c> dell'immagine.</summary>
    public string Url => UrlPrefix + MediaId;

    private sealed class Dto
    {
        public string? mediaId { get; set; }
        public string? alt { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int scale { get; set; }
    }

    /// <summary>Legge il riferimento dal JSON del blocco; null se manca, è illeggibile o non porta uno sha.</summary>
    public static MediaRef? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(json);
            var id = dto?.mediaId?.Trim();
            if (string.IsNullOrEmpty(id)) return null;
            return new MediaRef(id, string.IsNullOrWhiteSpace(dto!.alt) ? null : dto.alt!.Trim(), dto.width, dto.height,
                ClampScale(dto.scale));
        }
        catch (JsonException)
        {
            // Un blocco immagine con JSON rotto si comporta come un blocco senza immagine: si vede il segnaposto
            // nell'editor, non un'eccezione in mezzo a un documento.
            return null;
        }
    }

    public static string Serialize(MediaRef media) =>
        JsonSerializer.Serialize(new Dto
        {
            mediaId = media.MediaId,
            alt = media.Alt,
            width = media.Width,
            height = media.Height,
            scale = ClampScale(media.Scale),
        });

    /// <summary>Testo indicizzabile/leggibile di un blocco immagine: alternativo e didascalia, mai il JSON.</summary>
    public static string TextOf(string? json, string? caption)
    {
        var alt = Parse(json)?.Alt;
        return string.Join(" ", new[] { alt, caption }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
