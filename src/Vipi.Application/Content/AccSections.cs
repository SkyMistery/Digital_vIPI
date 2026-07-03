namespace Vipi.Application.Content;

/// <summary>
/// Registry delle sezioni fisse dei blocchi della vIPI ACC + riconciliazione pura dell'ordine.
/// Due registry: <see cref="Aerovia"/> (blocco settori CTR) e <see cref="AppBlock"/> (blocco gruppo-APP).
/// Riusa <see cref="AppSectionDescriptor"/>/<see cref="AppSectionKind"/> così che <c>SectionShell</c> resti condiviso.
/// La logica di <see cref="Reconcile"/> è identica a quella di <see cref="AppSections"/>, parametrizzata sul registry.
/// </summary>
public static class AccSections
{
    /// <summary>Sezioni del blocco Aerovia (settori CTR dell'ACC). Da seed RomaContentSeed.</summary>
    public static readonly IReadOnlyList<AppSectionDescriptor> Aerovia = new[]
    {
        new AppSectionDescriptor("separations",    "Separazioni radar",         1, AppSectionKind.Editorial),
        new AppSectionDescriptor("configurations", "Configurazioni",            2, AppSectionKind.Editorial),
        new AppSectionDescriptor("aor",            "AOR",                       3, AppSectionKind.Derived),
        new AppSectionDescriptor("frequencies",    "Frequenze",                 4, AppSectionKind.Derived),
        new AppSectionDescriptor("minima",         "Minime di vettoramento",    5, AppSectionKind.Derived),
        new AppSectionDescriptor("coordination",   "Coordinamenti",             6, AppSectionKind.Derived),
        new AppSectionDescriptor("regulated",      "Aree regolamentate",        7, AppSectionKind.Editorial),
    };

    /// <summary>Sezioni di un blocco gruppo-APP (settori APP scelti). Più snello dell'Aerovia.</summary>
    public static readonly IReadOnlyList<AppSectionDescriptor> AppBlock = new[]
    {
        new AppSectionDescriptor("separations",    "Separazioni",               1, AppSectionKind.Editorial),
        new AppSectionDescriptor("configurations", "Configurazioni",            2, AppSectionKind.Editorial),
        new AppSectionDescriptor("aor",            "AOR",                       3, AppSectionKind.Derived),
        new AppSectionDescriptor("frequencies",    "Frequenze",                 4, AppSectionKind.Derived),
        new AppSectionDescriptor("vfr",            "VFR",                       5, AppSectionKind.Editorial),
        new AppSectionDescriptor("coordination",   "Coordinamenti",             6, AppSectionKind.Derived),
    };

    /// <summary>Registry per tipo di blocco.</summary>
    public static IReadOnlyList<AppSectionDescriptor> For(AccBlockKind kind) =>
        kind == AccBlockKind.Aerovia ? Aerovia : AppBlock;

    public static bool IsFixed(IReadOnlyList<AppSectionDescriptor> registry, string key) =>
        registry.Any(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));

    public static AppSectionDescriptor? Find(IReadOnlyList<AppSectionDescriptor> registry, string key) =>
        registry.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Riconcilia l'ordine salvato col registry del blocco: scarta le chiavi non valide, preserva l'ordine
    /// salvato delle valide, inserisce le sezioni fisse mancanti al loro DefaultOrder, accoda le custom residue.
    /// Pura e deterministica. Identica ad <see cref="AppSections.Reconcile"/>, parametrizzata sul registry.
    /// </summary>
    public static IReadOnlyList<string> Reconcile(
        IReadOnlyList<AppSectionDescriptor> registry, IReadOnlyList<string> savedOrder, IReadOnlySet<string>? customKeys = null)
    {
        var custom = customKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();
        foreach (var k in savedOrder)
            if ((IsFixed(registry, k) || custom.Contains(k)) && seen.Add(k))
                kept.Add(k);

        foreach (var desc in registry.OrderBy(d => d.DefaultOrder))
        {
            if (seen.Contains(desc.Key)) continue;
            var idx = kept.Count;
            for (var i = 0; i < kept.Count; i++)
                if (Find(registry, kept[i]) is { } f && f.DefaultOrder > desc.DefaultOrder) { idx = i; break; }
            kept.Insert(idx, desc.Key);
            seen.Add(desc.Key);
        }

        foreach (var k in custom.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (seen.Add(k)) kept.Add(k);

        return kept;
    }
}
