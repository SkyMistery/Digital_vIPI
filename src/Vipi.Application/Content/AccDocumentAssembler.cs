using System.Text.Json;

namespace Vipi.Application.Content;

/// <summary>Un blocco della vIPI ACC assemblato da un <see cref="EditableDocument"/>: identità sezione + il modello
/// <see cref="AccBlock"/> (solo i campi che guidano le derivazioni) + la mappa chiave-sezione → Id per i salvataggi
/// editoriali by-section. Doc refactor 08e-acc.</summary>
public sealed record AccAssembledBlock(int BlockSectionId, AccBlock Block, IReadOnlyDictionary<string, int> ChildSectionIdsByKey);

/// <summary>
/// Ricostruisce i blocchi della vIPI ACC dall'albero <see cref="DocumentSection"/> del Document (doc refactor 08e-acc,
/// Opzione A): ogni sezione radice (depth 0) è un blocco, con le sue sezioni-catalogo come figlie. Puro/deterministico:
/// il metadata del blocco viene dal <c>BodyJson</c> del blocco proprio della sezione-blocco (schema
/// <see cref="AccBlockMeta"/>); configurazioni e aree attaccate dai <c>BodyJson</c> delle figlie <c>configurations</c>/
/// <c>regulated</c>. Assembla SOLO i campi che servono alle derivazioni (membri/config/override); il contenuto
/// editoriale (separations/vfr/prosa) è reso a parte leggendo direttamente le sezioni del Document.
/// </summary>
public static class AccDocumentAssembler
{
    public static IReadOnlyList<AccAssembledBlock> Assemble(EditableDocument doc) => Assemble(doc.Sections);

    public static IReadOnlyList<AccAssembledBlock> Assemble(IReadOnlyList<EditableSection> roots)
    {
        var result = new List<AccAssembledBlock>();
        foreach (var blockSection in roots.OrderBy(s => s.Order))
        {
            var meta = Deserialize<AccBlockMeta>(OwnBodyJson(blockSection));
            var kind = meta?.Kind
                ?? (string.Equals(blockSection.SectionKey, "aerovia", StringComparison.OrdinalIgnoreCase)
                    ? AccBlockKind.Aerovia : AccBlockKind.AppGroup);

            var childIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in blockSection.Children.OrderBy(c => c.Order))
                childIds.TryAdd(c.SectionKey, c.Id);

            var configs = Deserialize<List<AccConfiguration>>(ChildBodyJson(blockSection, "configurations")) ?? new();
            var attached = Deserialize<List<string>>(ChildBodyJson(blockSection, "regulated")) ?? new();

            var block = new AccBlock
            {
                Key = string.IsNullOrWhiteSpace(meta?.Key) ? blockSection.SectionKey : meta!.Key,
                Kind = kind,
                Title = blockSection.Title,
                MemberCallsigns = meta?.MemberCallsigns ?? new(),
                HiddenSections = meta?.HiddenSections ?? new(),
                FreqOrder = meta?.FreqOrder ?? new(),
                FreqLinkCallsigns = meta?.FreqLinkCallsigns ?? new(),
                CoordinationSentenceTemplate = meta?.CoordinationSentenceTemplate,
                Configurations = configs,
                AttachedSpecialAreaIds = attached,
                SectionOrder = blockSection.Children.OrderBy(c => c.Order).Select(c => c.SectionKey).ToList(),
            };
            result.Add(new AccAssembledBlock(blockSection.Id, block, childIds));
        }
        return result;
    }

    // BodyJson del blocco proprio della sezione (blockmeta): primo blocco della sezione-blocco stessa.
    private static string? OwnBodyJson(EditableSection s) => s.Blocks.OrderBy(b => b.Order).FirstOrDefault()?.BodyJson;

    // BodyJson del primo blocco della sezione figlia con la chiave data.
    private static string? ChildBodyJson(EditableSection parent, string key) =>
        parent.Children.Where(c => string.Equals(c.SectionKey, key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Order)
            .SelectMany(c => c.Blocks.OrderBy(b => b.Order))
            .Select(b => b.BodyJson)
            .FirstOrDefault();

    private static T? Deserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
    }
}
