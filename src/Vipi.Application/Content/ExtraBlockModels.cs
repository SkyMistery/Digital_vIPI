using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

// Blocchi editoriali delle "Sezioni extra" dell'aeroporto: stesso vocabolario del vIPI editor
// (Prosa/Callout/Tabella). Persistiti come JSON dentro AirportExtraSection.Body — nessuna nuova colonna
// né migrazione: un Body legacy (markdown semplice) viene letto come UN singolo blocco prosa (retro-compat).

/// <summary>Un blocco di una sezione extra. <see cref="Kind"/> del callout usato solo se Format=Callout;
/// <see cref="TableJson"/> (columns/rows) usato solo se Format=Table; <see cref="Text"/> negli altri casi.</summary>
public sealed class ExtraBlock
{
    public BlockFormat Format { get; set; } = BlockFormat.Prose;
    public string? Text { get; set; }
    public CalloutKind CalloutKind { get; set; } = CalloutKind.Info;
    public string? TableJson { get; set; }   // {"columns":[...],"rows":[{"cells":[...]}]} — stesso formato di DocumentSectionsEditor
}

/// <summary>Serializzazione robusta dei blocchi di una sezione extra dentro il campo Body (JSON), con fallback legacy.</summary>
public static class ExtraBlocks
{
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

    private sealed class Envelope { public List<ExtraBlock> blocks { get; set; } = new(); }

    /// <summary>Deserializza il Body in blocchi. Body vuoto → nessun blocco; JSON {"blocks":[...]} → i blocchi;
    /// qualunque altro testo → UN blocco prosa col testo (retro-compatibilità con gli extra markdown esistenti).</summary>
    public static List<ExtraBlock> Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return new();
        var t = body.TrimStart();
        if (t.StartsWith("{"))
        {
            try
            {
                var env = JsonSerializer.Deserialize<Envelope>(body, Opts);
                if (env?.blocks is { } bl) return bl.Select(Normalize).ToList();
            }
            catch (JsonException) { /* non è il nostro envelope: trattalo come markdown legacy */ }
        }
        return new() { new ExtraBlock { Format = BlockFormat.Prose, Text = body } };
    }

    /// <summary>Serializza i blocchi in JSON per il campo Body. Lista vuota → stringa vuota (sezione senza corpo).</summary>
    public static string? Serialize(IReadOnlyList<ExtraBlock> blocks)
    {
        var clean = blocks.Where(NotEmpty).Select(Normalize).ToList();
        return clean.Count == 0 ? null : JsonSerializer.Serialize(new Envelope { blocks = clean }, Opts);
    }

    /// <summary>Testo di anteprima/ricerca: concatena il testo dei blocchi prosa/callout (le tabelle non entrano).</summary>
    public static string PlainText(string? body) =>
        string.Join(" ", Parse(body)
            .Where(b => b.Format is BlockFormat.Prose or BlockFormat.List or BlockFormat.Callout)
            .Select(b => b.Text)
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    private static ExtraBlock Normalize(ExtraBlock b) => new()
    {
        Format = b.Format is BlockFormat.Prose or BlockFormat.List or BlockFormat.Callout or BlockFormat.Table ? b.Format : BlockFormat.Prose,
        Text = b.Text,
        CalloutKind = b.CalloutKind,
        TableJson = b.TableJson,
    };

    private static bool NotEmpty(ExtraBlock b) => b.Format == BlockFormat.Table
        ? !string.IsNullOrWhiteSpace(b.TableJson)
        : !string.IsNullOrWhiteSpace(b.Text);
}
