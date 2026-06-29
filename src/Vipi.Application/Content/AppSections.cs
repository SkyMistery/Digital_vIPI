namespace Vipi.Application.Content;

/// <summary>Natura di una sezione APP: editoriale (salvata) o derivata live (calcolata da albero/transfer/poligono).</summary>
public enum AppSectionKind { Editorial, Derived }

/// <summary>
/// Descrittore di una sezione fissa dell'APP standalone (fonte di verità in codice, <see cref="AppSections.All"/>).
/// Aggiungere una nuova sezione obbligatoria = aggiungere un descrittore qui: la riconciliazione al load la inserisce
/// in tutti i profili esistenti al suo <see cref="DefaultOrder"/>, senza migrazione dati.
/// </summary>
public sealed record AppSectionDescriptor(string Key, string Title, int DefaultOrder, AppSectionKind Kind);

/// <summary>Registry delle 6 sezioni fisse dell'APP + riconciliazione pura dell'ordine col profilo salvato.</summary>
public static class AppSections
{
    /// <summary>Le 6 sezioni fisse, sempre presenti, riordinabili. L'ordine qui è il default (DefaultOrder crescente).</summary>
    public static readonly IReadOnlyList<AppSectionDescriptor> All = new[]
    {
        new AppSectionDescriptor("separations",  "Separazioni",             1, AppSectionKind.Editorial),
        new AppSectionDescriptor("aor",          "AOR",                     2, AppSectionKind.Derived),
        new AppSectionDescriptor("frequencies",  "Frequenze",               3, AppSectionKind.Derived),
        new AppSectionDescriptor("vfr",          "VFR",                     4, AppSectionKind.Editorial),
        new AppSectionDescriptor("minima",       "Minime di vettoramento",  5, AppSectionKind.Derived),
        new AppSectionDescriptor("coordination", "Coordinamenti",           6, AppSectionKind.Derived),
    };

    private static readonly Dictionary<string, AppSectionDescriptor> ByKey =
        All.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Vero se la chiave appartiene a una sezione fissa del registry.</summary>
    public static bool IsFixed(string key) => ByKey.ContainsKey(key);

    public static AppSectionDescriptor? Find(string key) => ByKey.TryGetValue(key, out var d) ? d : null;

    /// <summary>
    /// Riconcilia l'ordine salvato con il registry: scarta le chiavi non più valide (fisse rimosse o custom inesistenti),
    /// preserva l'ordine salvato delle chiavi valide, e inserisce ogni sezione fissa mancante al suo
    /// <see cref="AppSectionDescriptor.DefaultOrder"/> relativo alle altre fisse. Pura e deterministica.
    /// </summary>
    /// <param name="savedOrder">Ordine persistito (può essere vuoto/parziale/contenere chiavi stale).</param>
    /// <param name="customKeys">Chiavi delle sezioni custom realmente presenti (preservate al loro posto). null = nessuna.</param>
    public static IReadOnlyList<string> Reconcile(IReadOnlyList<string> savedOrder, IReadOnlySet<string>? customKeys = null)
    {
        var custom = customKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1 — Mantieni l'ordine salvato delle sole chiavi valide (fissa nota o custom esistente), senza duplicati.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();
        foreach (var k in savedOrder)
            if ((IsFixed(k) || custom.Contains(k)) && seen.Add(k))
                kept.Add(k);

        // 2 — Inserisci le sezioni fisse mancanti al loro DefaultOrder (rispetto alle altre fisse già presenti).
        foreach (var desc in All.OrderBy(d => d.DefaultOrder))
        {
            if (seen.Contains(desc.Key)) continue;
            var idx = kept.Count;
            for (var i = 0; i < kept.Count; i++)
                if (Find(kept[i]) is { } f && f.DefaultOrder > desc.DefaultOrder) { idx = i; break; }
            kept.Insert(idx, desc.Key);
            seen.Add(desc.Key);
        }

        // 3 — Eventuali custom non citate nell'ordine salvato vanno in coda (deterministico per ordine alfabetico).
        foreach (var k in custom.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (seen.Add(k)) kept.Add(k);

        return kept;
    }
}
