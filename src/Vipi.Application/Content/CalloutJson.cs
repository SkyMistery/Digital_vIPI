using System.Text.Json;
using System.Text.Json.Nodes;

namespace Vipi.Application.Content;

/// <summary>
/// Il JSON di un blocco <b>callout</b> (<c>ContentBlock.BodyJson</c>, <c>ExtraBlock.CalloutJson</c>):
/// <c>{"title":"…","allegati":[{"ref":"allegato:…",…},…]}</c>, tutte e due le chiavi facoltative.
///
/// <para>Gli allegati (S121, 21 ottobre 2026, committente: «nei callout vorrei la possibilità di inserire allegati»)
/// hanno la forma di quello del blocco «Allegato» (<see cref="AttachmentRef"/>): la stessa stringa, letta e scritta
/// dalla stessa classe, così modo, altezza e rotazione valgono uguali nei due posti.</para>
///
/// <para>⚠️ Gli allegati si mettono in JSON come <c>allegato:slug</c>, e il registro «chi cita cosa»
/// (<c>AttachmentReferenceScanner</c>) li conta da solo: legge il JSON grezzo di ogni blocco.</para>
/// </summary>
public static class CalloutJson
{
    public const string ChiaveTitolo = "title";
    public const string ChiaveAllegati = "allegati";

    /// <summary>Il titolo scritto, o <c>null</c>.</summary>
    public static string? Titolo(string? json) =>
        Leggi(json) is { } o && o[ChiaveTitolo] is JsonValue v && v.TryGetValue<string>(out var s)
        && !string.IsNullOrWhiteSpace(s) ? s : null;

    /// <summary>Gli allegati, nell'ordine scritto. Uno illeggibile si salta: un callout non si rompe per un allegato.</summary>
    public static IReadOnlyList<AttachmentRef> Allegati(string? json)
    {
        if (Leggi(json)?[ChiaveAllegati] is not JsonArray lista) return Array.Empty<AttachmentRef>();
        return lista
            .Select(n => n is JsonObject ? AttachmentRef.Parse(n.ToJsonString()) : null)
            .OfType<AttachmentRef>()
            .ToList();
    }

    /// <summary>
    /// Il JSON con questi allegati al posto di quelli che aveva; il titolo (e ogni altra chiave) resta.
    /// Torna <c>""</c> quando non resta niente: per gli editor la stringa vuota vuol dire «togli», e <c>null</c>
    /// «non toccare».
    /// </summary>
    public static string ConAllegati(string? json, IReadOnlyList<AttachmentRef> allegati)
    {
        var o = Leggi(json) ?? new JsonObject();
        if (allegati.Count == 0) o.Remove(ChiaveAllegati);
        else o[ChiaveAllegati] = new JsonArray(allegati.Select(a => JsonNode.Parse(AttachmentRef.Serialize(a))).ToArray());
        return o.Count == 0 ? "" : o.ToJsonString();
    }

    /// <summary>
    /// Vero se la radice ha la forma di un callout: solo <c>title</c> (testo) e/o <c>allegati</c> (elenco), e almeno
    /// una delle due. La usa <see cref="SectionPayload.EEditoriale"/>: un callout non è il payload di una scheda.
    /// </summary>
    public static bool EForma(JsonElement radice)
    {
        if (radice.ValueKind != JsonValueKind.Object) return false;
        var chiavi = 0;
        foreach (var p in radice.EnumerateObject())
        {
            chiavi++;
            var buona = p.Name switch
            {
                ChiaveTitolo => p.Value.ValueKind == JsonValueKind.String,
                ChiaveAllegati => p.Value.ValueKind == JsonValueKind.Array,
                _ => false,
            };
            if (!buona) return false;
        }
        return chiavi > 0;
    }

    private static JsonObject? Leggi(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
    }
}
