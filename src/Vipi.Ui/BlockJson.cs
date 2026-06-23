using System.Text.Json;

namespace Vipi.Ui;

/// <summary>Utility per leggere il discriminatore "variant" dei blocchi con BodyJson.</summary>
public static class BlockJson
{
    public static string? Variant(string? bodyJson)
    {
        if (string.IsNullOrWhiteSpace(bodyJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(bodyJson);
            return doc.RootElement.TryGetProperty("variant", out var v) ? v.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
