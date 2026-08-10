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
            ["sids"] = SectionKind.Derived,   // aeroporto (doc 10 §3e): SID derivata a view-time, non più cotta
            // «minima» è Editorial dal doc 13 §3b: le MVA non si importano (decisione 2026-08-09, lavori-aperti §E2)
            // e si scrivono a mano. Da derivata non derivava nulla: nessun provider la catturava, il toggle
            // Live/Congelata non aveva effetto e — peggio — l'editor non offriva i blocchi, quindi era l'unica
            // sezione del documento in cui NON si poteva scrivere.
            ["minima"] = SectionKind.Editorial,
            ["purpose"] = SectionKind.Editorial,   // vLOA: scopo dell'accordo, prosa (doc 13 §3c)
            ["separations"] = SectionKind.Editorial,
            ["configurations"] = SectionKind.Editorial,
            ["vfr"] = SectionKind.Editorial,
            ["regulated"] = SectionKind.Editorial,
            ["operationaltechnique"] = SectionKind.Editorial,
            ["validity"] = SectionKind.Editorial,
        };

    /// <summary>Natura della sezione con questa chiave (Editorial se sconosciuta = custom).</summary>
    public static SectionKind KindOf(string key) => KindByKey.TryGetValue(key, out var k) ? k : SectionKind.Editorial;

    // Sezioni che nascono COLLASSATE (doc 11 §3i): quelle il cui contenuto è voluminoso per natura — «Aree
    // regolamentate» su una ACC sono decine di aree, ognuna con la sua mappa, e aperta la sezione occupa il
    // documento da sola. Vale OVUNQUE: viewer ed editor, tutte e tre le famiglie.
    private static readonly IReadOnlySet<string> InitiallyCollapsedKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "regulated" };

    /// <summary>Vero se la sezione si apre COLLASSATA nel documento: si espande a mano (doc 11 §3i).</summary>
    public static bool IsInitiallyCollapsed(string key) => InitiallyCollapsedKeys.Contains(key);

    /// <summary>
    /// Vero se la sezione espone all'editor il toggle Live/Frozen (doc 10 §3a): solo le sezioni DERIVATE, perché
    /// per quelle editoriali non esiste una derivazione da congelare. La regola stava ripetuta identica nei tre
    /// editor (ACC, APP, vLOA) e vive qui, dove è definita la natura delle sezioni.
    /// </summary>
    public static bool IsRenderModeToggleable(string key) => KindOf(key) == SectionKind.Derived;

    // Corpo prodotto dalla PAGINA (doc 13 §3a): derivate + editoriali-strutturate. Scritto per esteso su ogni
    // voce perché non è deducibile dalla natura — «regulated» è un picker sulla vIPI ACC/APP e prosa sulla vLOA.
    private static SectionDescriptor D(string key, string title, int order) =>
        new(key, title, order, KindOf(key), SectionBodySource.Blocks);

    private static SectionDescriptor H(string key, string title, int order) =>
        new(key, title, order, KindOf(key), SectionBodySource.Host);

    // Membership per profilo (key, titolo, ordine). Universali a tutti: aor/frequencies/coordination/regulated/
    // operationaltechnique/validity. ACC/APP in italiano, vLOA in inglese (lettera di accordo bilaterale).
    // H(...) = corpo reso dalla pagina, D(...) = corpo dai blocchi della sezione.
    private static readonly IReadOnlyDictionary<SectionProfile, IReadOnlyList<SectionDescriptor>> Registry =
        new Dictionary<SectionProfile, IReadOnlyList<SectionDescriptor>>
        {
            [SectionProfile.App] = new[]
            {
                H("separations", "Separazioni", 1),
                H("configurations", "Configurazioni", 2),
                H("aor", "AOR", 3),
                H("frequencies", "Frequenze", 4),
                D("minima", "Minime di vettoramento", 5),
                H("vfr", "VFR", 6),
                H("coordination", "Coordinamenti", 7),
                H("regulated", "Aree regolamentate", 8),
                D("operationaltechnique", "Procedure generali", 9),
                D("validity", "Validità e revisione", 10),
            },
            [SectionProfile.AccAerovia] = new[]
            {
                H("separations", "Separazioni radar", 1),
                H("configurations", "Configurazioni", 2),
                H("aor", "AOR", 3),
                H("frequencies", "Frequenze", 4),
                D("minima", "Minime di vettoramento", 5),
                H("coordination", "Coordinamenti", 7),
                H("regulated", "Aree regolamentate", 8),
                D("operationaltechnique", "Procedure generali", 9),
                D("validity", "Validità e revisione", 10),
            },
            [SectionProfile.AccAppBlock] = new[]
            {
                H("separations", "Separazioni", 1),
                H("configurations", "Configurazioni", 2),
                H("aor", "AOR", 3),
                H("frequencies", "Frequenze", 4),
                D("minima", "Minime di vettoramento", 5),
                H("vfr", "VFR", 6),
                H("coordination", "Coordinamenti", 7),
                H("regulated", "Aree regolamentate", 8),
                D("operationaltechnique", "Procedure generali", 9),
                D("validity", "Validità e revisione", 10),
            },
            // vLOA: titoli e ORDINE sono quelli del documento reale (doc 13 §3c). Fino al doc 13 questo profilo non
            // lo leggeva nessuno — la struttura nasceva da VloaSections — e i due elenchi erano divergenti: mancava
            // «purpose», «General procedures» stava dopo «Coordination» e le aree si chiamavano «Regulated areas».
            [SectionProfile.Vloa] = new[]
            {
                D("purpose", "Purpose", 1),
                H("aor", "Areas of Responsibility", 2),
                H("frequencies", "Frequencies", 3),
                D("operationaltechnique", "General procedures", 4),
                H("coordination", "Coordination", 5),
                D("regulated", "Military areas coordination and management", 6),
                D("validity", "Validity and Revision", 7),
            },
        };

    /// <summary>
    /// Vero se il corpo di questa sezione lo produce la PAGINA e non i blocchi della sezione (doc 13 §3a): sezioni
    /// derivate ed editoriali-strutturate. È la domanda che viewer ed editor si fanno per decidere se rendere il
    /// contenuto documentale o cedere il posto al componente dedicato — stava ripetuta in sei insiemi di pagina.
    /// </summary>
    public static bool IsHostRendered(SectionProfile profile, string key) =>
        Find(profile, key)?.BodySource == SectionBodySource.Host;

    /// <summary>Profilo di catalogo di un blocco della vIPI ACC (Aerovia o gruppo APP): la corrispondenza stava
    /// scritta a mano nell'assembler e negli editor.</summary>
    public static SectionProfile ProfileOfAccBlock(AccBlockKind kind) =>
        kind == AccBlockKind.Aerovia ? SectionProfile.AccAerovia : SectionProfile.AccAppBlock;

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
