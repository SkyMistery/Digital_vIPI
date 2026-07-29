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

    /// <summary>Assembla i blocchi da uno snapshot di release (RawDocument): mappa l'albero grezzo a EditableSection e
    /// riusa l'assemblaggio. Gli Id sezione dello snapshot non servono ai salvataggi (vista sola-lettura). Doc 08e-acc.</summary>
    public static IReadOnlyList<AccAssembledBlock> Assemble(RawDocument raw) => Assemble(raw.Roots.Select(ToEditable).ToList());

    private static EditableSection ToEditable(RawSection s) => new()
    {
        Id = s.Id, Title = s.Title, SectionKey = s.SectionKey, Depth = s.Depth, Order = s.Order,
        Blocks = s.Blocks.OrderBy(b => b.Order).Select(b => new EditableBlock
        {
            Id = b.Id, Order = b.Order, Format = b.Format, Tier = b.Tier, Visibility = b.Visibility,
            CalloutKind = b.CalloutKind, Body = b.Body, BodyJson = b.BodyJson,
        }).ToList(),
        Children = s.Children.OrderBy(c => c.Order).Select(ToEditable).ToList(),
    };

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
            var regulated = ParseRegulated(ChildBodyJson(blockSection, "regulated"));
            var separations = Deserialize<List<AppSeparationRow>>(ChildBodyJson(blockSection, "separations")) ?? new();
            var vfrJson = ChildBodyJson(blockSection, "vfr");   // AppVfrContent grezzo (AccBlock.VfrJson è stringa)
            // Shape AoR extra + override colore dalla sezione figlia "aor" (editoriale). Negli snapshot frozen quel
            // BodyJson contiene invece l'AccAorView renderizzato: Deserialize<AorExtraShapes> ignora i campi estranei →
            // liste vuote (ok: la vista frozen usa lo snapshot congelato, non ri-deriva).
            var aorCustom = Deserialize<AorExtraShapes>(ChildBodyJson(blockSection, "aor")) ?? new();
            var customs = CustomSectionsOf(blockSection);

            var block = new AccBlock
            {
                Key = string.IsNullOrWhiteSpace(meta?.Key) ? blockSection.SectionKey : meta!.Key,
                Kind = kind,
                Title = blockSection.Title,
                MemberCallsigns = meta?.MemberCallsigns ?? new(),
                HiddenSections = meta?.HiddenSections ?? new(),
                FreqOrder = meta?.FreqOrder ?? new(),
                FreqLinkCallsigns = meta?.FreqLinkCallsigns ?? new(),
                Configurations = configs,
                ExtraAorCallsigns = aorCustom.Callsigns ?? new(),
                AorColorOverrides = aorCustom.Colors ?? new(),
                Regulated = regulated,
                Separations = separations,
                VfrJson = vfrJson,
                CustomSections = customs,
                SectionOrder = blockSection.Children.OrderBy(c => c.Order).Select(c => c.SectionKey).ToList(),
            };
            result.Add(new AccAssembledBlock(blockSection.Id, block, childIds));
        }
        return result;
    }

    // Chiavi rese in modo speciale (derivate o editoriali-strutturate): NON sono sezioni custom editoriali generiche.
    private static readonly HashSet<string> StructuredKeys = new(StringComparer.OrdinalIgnoreCase)
        { "separations", "configurations", "aor", "frequencies", "minima", "vfr", "coordination", "regulated" };

    // Sezioni editoriali generiche del blocco (operationaltechnique/validity/custom:*): figli con chiave non-strutturata,
    // rese come AppCustomSection (prosa dai blocchi). Preserva l'ordine; la chiave = SectionKey del figlio.
    private static List<AppCustomSection> CustomSectionsOf(EditableSection blockSection) =>
        blockSection.Children.OrderBy(c => c.Order)
            .Where(c => !StructuredKeys.Contains(c.SectionKey))
            .Select(c => new AppCustomSection(c.SectionKey, c.Title,
                c.Blocks.OrderBy(b => b.Order)
                    .Where(b => !string.IsNullOrWhiteSpace(b.Body))
                    .Select(b => new AppCustomBlock(AppCustomBlockType.Prose, b.Body, null, null))
                    .ToList()))
            .ToList();

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

    // Aree regolamentate: back-compat. null/vuoto = automatico (unset); array legacy ["id",…] = manuale con quegli id
    // (conservativo: preserva l'insieme mostrato prima); oggetto = RegulatedSelection nativo.
    private static RegulatedSelection ParseRegulated(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new RegulatedSelection { OwnAuto = true };
        var trimmed = json.TrimStart();
        try
        {
            if (trimmed.StartsWith("["))
                return new RegulatedSelection { OwnAuto = false, OwnIds = JsonSerializer.Deserialize<List<string>>(json) ?? new() };
            return JsonSerializer.Deserialize<RegulatedSelection>(json) ?? new RegulatedSelection { OwnAuto = true };
        }
        catch (JsonException) { return new RegulatedSelection { OwnAuto = true }; }
    }
}
