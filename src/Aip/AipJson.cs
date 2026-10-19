using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aip;

/// <summary>
/// Il JSON del modello: lo stesso file per l'AIP espanso, per il converter e, compresso, per il vSOP (carta F6,
/// D8). Chiavi in camelCase, enum come parole (<c>"restricted"</c>, <c>"clockwise"</c>), i campi vuoti non si
/// scrivono.
/// <para>🔴 La versione si legge <b>prima</b> del resto: un file di uno schema più nuovo dice «schema 3, so
/// leggere fino al 2», non un errore a caso a metà del contenuto.</para>
/// </summary>
public static class AipJson
{
    public static JsonSerializerOptions Options { get; } = CreaOpzioni();

    public static string Serialize(AipDocument document) => JsonSerializer.Serialize(document, Options);

    public static void Serialize(Stream stream, AipDocument document) =>
        JsonSerializer.Serialize(stream, document, Options);

    public static AipDocument Deserialize(string json)
    {
        JsonDocument letto;
        try
        {
            letto = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new AipJsonException(AipJsonError.NotJson, e.Message, e);
        }

        using (letto)
            return Deserialize(letto);
    }

    public static AipDocument Deserialize(Stream stream)
    {
        JsonDocument letto;
        try
        {
            letto = JsonDocument.Parse(stream);
        }
        catch (JsonException e)
        {
            throw new AipJsonException(AipJsonError.NotJson, e.Message, e);
        }

        using (letto)
            return Deserialize(letto);
    }

    private static AipDocument Deserialize(JsonDocument letto)
    {
        var radice = letto.RootElement;
        if (radice.ValueKind != JsonValueKind.Object
            || !radice.TryGetProperty("schemaVersion", out var versione)
            || versione.ValueKind != JsonValueKind.Number
            || !versione.TryGetInt32(out var numero))
            throw new AipJsonException(AipJsonError.MissingSchemaVersion);

        if (numero < 1 || numero > AipDocument.CurrentSchemaVersion)
            throw new AipJsonException(AipJsonError.UnsupportedSchemaVersion, numero.ToString(System.Globalization.CultureInfo.InvariantCulture));

        try
        {
            return radice.Deserialize<AipDocument>(Options)
                ?? throw new AipJsonException(AipJsonError.Invalid, "null");
        }
        catch (JsonException e)
        {
            throw new AipJsonException(AipJsonError.Invalid, e.Message, e);
        }
        catch (NotSupportedException e)
        {
            // Un `kind` mancante o sconosciuto su un tipo astratto arriva così, non come JsonException.
            throw new AipJsonException(AipJsonError.Invalid, e.Message, e);
        }
    }

    private static JsonSerializerOptions CreaOpzioni()
    {
        var opzioni = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Il Web di default accetta i numeri fra virgolette: un modello che si rilegge identico non lo vuole.
            NumberHandling = JsonNumberHandling.Strict,
            // Il testo com'è (`Zona/Zone "2"`, `attività`), non `"` e `à`: l'AIP espanso si legge nei diff di
            // GitHub. È un file di dati, mai messo in una pagina HTML: l'«unsafe» del nome riguarda solo quello.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        opzioni.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        opzioni.MakeReadOnly(populateMissingResolver: true);
        return opzioni;
    }
}

/// <summary>Perché un file non è un modello AIP. ⚠️ Un codice, non una frase: il testo lo scrive chi lo mostra.</summary>
public enum AipJsonError
{
    /// <summary>Non è JSON.</summary>
    NotJson,

    /// <summary>Manca <c>schemaVersion</c> (o non è un intero).</summary>
    MissingSchemaVersion,

    /// <summary>Lo schema è più nuovo di quel che questo codice sa leggere. <c>Detail</c> = la versione del file.</summary>
    UnsupportedSchemaVersion,

    /// <summary>È JSON dello schema giusto, ma il contenuto non torna (un campo obbligatorio, un <c>kind</c> sconosciuto…).</summary>
    Invalid,
}

public sealed class AipJsonException(AipJsonError error, string? detail = null, Exception? inner = null)
    : Exception($"{error}{(detail is null ? "" : ": " + detail)}", inner)
{
    public AipJsonError Error { get; } = error;

    public string? Detail { get; } = detail;
}
