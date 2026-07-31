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
public sealed record MediaRef(string MediaId, string? Alt = null, int Width = 0, int Height = 0)
{
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
            return new MediaRef(id, string.IsNullOrWhiteSpace(dto!.alt) ? null : dto.alt!.Trim(), dto.width, dto.height);
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
        });

    /// <summary>Testo indicizzabile/leggibile di un blocco immagine: alternativo e didascalia, mai il JSON.</summary>
    public static string TextOf(string? json, string? caption)
    {
        var alt = Parse(json)?.Alt;
        return string.Join(" ", new[] { alt, caption }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }
}
