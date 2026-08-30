using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

// Blocchi editoriali delle vecchie "Sezioni extra" dell'aeroporto: stesso vocabolario del vIPI editor
// (Prosa/Callout/Tabella). Erano persistiti come JSON dentro AirportExtraSection.Body; dalla carta 2026-08-26
// quel corpo lo legge solo il trasloco one-shot, che lo riversa nei blocchi del documento — nessuna colonna
// né migrazione: un Body legacy (markdown semplice) viene letto come UN singolo blocco prosa (retro-compat).

/// <summary>Un blocco di una sezione extra. <see cref="CalloutKind"/> usato solo se Format=Callout;
/// <see cref="TableJson"/> (columns/rows) usato solo se Format=Table; <see cref="ImageJson"/> solo se Format=Image
/// (e lì <see cref="Text"/> è la didascalia); <see cref="Text"/> negli altri casi.</summary>
public sealed class ExtraBlock
{
    public BlockFormat Format { get; set; } = BlockFormat.Prose;
    public string? Text { get; set; }
    public CalloutKind CalloutKind { get; set; } = CalloutKind.Info;
    public string? TableJson { get; set; }   // {"columns":[...],"rows":[{"cells":[...]}]} — stesso formato di DocumentSectionsEditor
    /// <summary>{"mediaId":…,"alt":…} — stessa stringa che nel documento sta in <c>ContentBlock.BodyJson</c>, letta e
    /// scritta da <see cref="MediaRef"/>: un formato solo per entrambi i mondi, come già fa <see cref="TableJson"/>.</summary>
    public string? ImageJson { get; set; }

    /// <summary>{"ref":"allegato:…","titolo":…} — stessa stringa che nel documento sta in
    /// <c>ContentBlock.BodyJson</c>, letta e scritta da <see cref="AttachmentRef"/>. Usato solo se
    /// Format=Attachment, e lì <see cref="Text"/> è la nota sotto il link.</summary>
    public string? AttachmentJson { get; set; }
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

    /// <summary>Testo di anteprima/ricerca: testo dei blocchi prosa/callout più alt e didascalia delle immagini
    /// (le tabelle non entrano, e il JSON dell'immagine non deve MAI finire in un'anteprima).</summary>
    public static string PlainText(string? body) =>
        string.Join(" ", Parse(body)
            .Select(b => b.Format switch
            {
                BlockFormat.Prose or BlockFormat.List or BlockFormat.Callout => b.Text,
                BlockFormat.Image => MediaRef.TextOf(b.ImageJson, b.Text),
                // Il titolo e la nota, mai il JSON: cercare «Marseille» deve trovare la LoA, e cercare un
                // pezzo di slug non deve far comparire una riga di JSON in un'anteprima.
                BlockFormat.Attachment => AttachmentRef.TextOf(b.AttachmentJson, b.Text),
                _ => null,
            })
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    private static ExtraBlock Normalize(ExtraBlock b) => new()
    {
        Format = b.Format is BlockFormat.Prose or BlockFormat.List or BlockFormat.Callout or BlockFormat.Table
            or BlockFormat.Image or BlockFormat.Attachment ? b.Format : BlockFormat.Prose,
        Text = b.Text,
        CalloutKind = b.CalloutKind,
        TableJson = b.TableJson,
        ImageJson = b.ImageJson,
        AttachmentJson = b.AttachmentJson,
    };

    // Un blocco «vuoto» non viene salvato. Per l'immagine il contenuto è il riferimento, non il testo: una foto senza
    // didascalia è legittima, una didascalia senza foto no.
    private static bool NotEmpty(ExtraBlock b) => b.Format switch
    {
        BlockFormat.Table => !string.IsNullOrWhiteSpace(b.TableJson),
        BlockFormat.Image => MediaRef.Parse(b.ImageJson) is not null,
        // Come per l'immagine: il contenuto è il RIFERIMENTO, non il testo. Un allegato senza nota è
        // legittimo, una nota senza allegato no.
        BlockFormat.Attachment => AttachmentRef.Parse(b.AttachmentJson) is not null,
        _ => !string.IsNullOrWhiteSpace(b.Text),
    };
}
