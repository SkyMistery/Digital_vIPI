namespace Vipi.Application.Content;

/// <summary>
/// Catalogo UNIFICATO delle sezioni documentali (doc refactor 08a). Fonte unica per: la natura di ogni sezione
/// (<see cref="KindOf"/>), la membership per profilo (<see cref="For"/>) e la riconciliazione d'ordine
/// (<see cref="Reconcile"/>, prima duplicata in <c>AppSections</c>/<c>AccSections</c>). Sostituisce i tre registry
/// per-tipo e l'enum <c>BlockSection</c>. L'aeroporto NON partecipa (documento generato a struttura propria).
/// </summary>
public static class SectionCatalog
{
    // Natura di ogni sezione fissa — fonte unica: "aor" è Derived ovunque, ecc.
    private static readonly IReadOnlyDictionary<string, SectionKind> KindByKey =
        new Dictionary<string, SectionKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["aor"] = SectionKind.Derived,
            ["frequencies"] = SectionKind.Derived,
            ["coordination"] = SectionKind.Derived,
            ["minima"] = SectionKind.Derived,
            ["sids"] = SectionKind.Derived,   // aeroporto (doc 10 §3e): SID derivata a view-time, non più cotta
            ["separations"] = SectionKind.Editorial,
            ["configurations"] = SectionKind.Editorial,
            ["vfr"] = SectionKind.Editorial,
            ["regulated"] = SectionKind.Editorial,
            ["operationaltechnique"] = SectionKind.Editorial,
            ["validity"] = SectionKind.Editorial,
        };

    /// <summary>Natura della sezione con questa chiave (Editorial se sconosciuta = custom).</summary>
    public static SectionKind KindOf(string key) => KindByKey.TryGetValue(key, out var k) ? k : SectionKind.Editorial;

    private static SectionDescriptor D(string key, string title, int order) => new(key, title, order, KindOf(key));

    // Membership per profilo (key, titolo, ordine). Universali a tutti: aor/frequencies/coordination/regulated/
    // operationaltechnique/validity. ACC/APP in italiano, vLOA in inglese (lettera di accordo bilaterale).
    private static readonly IReadOnlyDictionary<SectionProfile, IReadOnlyList<SectionDescriptor>> Registry =
        new Dictionary<SectionProfile, IReadOnlyList<SectionDescriptor>>
        {
            [SectionProfile.App] = new[]
            {
                D("separations", "Separazioni", 1),
                D("configurations", "Configurazioni", 2),
                D("aor", "AOR", 3),
                D("frequencies", "Frequenze", 4),
                D("minima", "Minime di vettoramento", 5),
                D("vfr", "VFR", 6),
                D("coordination", "Coordinamenti", 7),
                D("regulated", "Aree regolamentate", 8),
                D("operationaltechnique", "Procedure generali", 9),
                D("validity", "Validità e revisione", 10),
            },
            [SectionProfile.AccAerovia] = new[]
            {
                D("separations", "Separazioni radar", 1),
                D("configurations", "Configurazioni", 2),
                D("aor", "AOR", 3),
                D("frequencies", "Frequenze", 4),
                D("minima", "Minime di vettoramento", 5),
                D("coordination", "Coordinamenti", 7),
                D("regulated", "Aree regolamentate", 8),
                D("operationaltechnique", "Procedure generali", 9),
                D("validity", "Validità e revisione", 10),
            },
            [SectionProfile.AccAppBlock] = new[]
            {
                D("separations", "Separazioni", 1),
                D("configurations", "Configurazioni", 2),
                D("aor", "AOR", 3),
                D("frequencies", "Frequenze", 4),
                D("minima", "Minime di vettoramento", 5),
                D("vfr", "VFR", 6),
                D("coordination", "Coordinamenti", 7),
                D("regulated", "Aree regolamentate", 8),
                D("operationaltechnique", "Procedure generali", 9),
                D("validity", "Validità e revisione", 10),
            },
            [SectionProfile.Vloa] = new[]
            {
                D("aor", "Areas of Responsibility", 3),
                D("frequencies", "Frequencies", 4),
                D("coordination", "Coordination", 7),
                D("regulated", "Regulated areas", 8),
                D("operationaltechnique", "General procedures", 9),
                D("validity", "Validity and Revision", 10),
            },
        };

    /// <summary>Sezioni fisse del profilo, in ordine di default.</summary>
    public static IReadOnlyList<SectionDescriptor> For(SectionProfile profile) => Registry[profile];

    public static SectionDescriptor? Find(SectionProfile profile, string key) =>
        For(profile).FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsFixed(SectionProfile profile, string key) => Find(profile, key) is not null;

    /// <summary>
    /// Riconcilia l'ordine salvato con il registry del profilo: scarta le chiavi non valide (fisse rimosse o custom
    /// inesistenti), preserva l'ordine salvato delle valide, inserisce le fisse mancanti al loro ordine di default,
    /// accoda le custom residue (alfabetico). Pura e deterministica. Unifica <c>AppSections</c>/<c>AccSections</c>.
    /// </summary>
    public static IReadOnlyList<string> Reconcile(
        SectionProfile profile, IReadOnlyList<string> savedOrder, IReadOnlySet<string>? customKeys = null)
    {
        var registry = For(profile);
        var custom = customKeys ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<string>();
        foreach (var k in savedOrder)
            if ((IsFixed(profile, k) || custom.Contains(k)) && seen.Add(k))
                kept.Add(k);

        foreach (var desc in registry.OrderBy(d => d.Order))
        {
            if (seen.Contains(desc.Key)) continue;
            var idx = kept.Count;
            for (var i = 0; i < kept.Count; i++)
                if (Find(profile, kept[i]) is { } f && f.Order > desc.Order) { idx = i; break; }
            kept.Insert(idx, desc.Key);
            seen.Add(desc.Key);
        }

        foreach (var k in custom.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            if (seen.Add(k)) kept.Add(k);

        return kept;
    }
}
