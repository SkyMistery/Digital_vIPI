using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Blocco di contenuto canonico di una sezione vLOA (spec puro, materializzato lato Infrastructure).</summary>
public sealed record VloaBlockSpec(BlockFormat Format, string? Body = null, string? BodyJson = null, CalloutKind? CalloutKind = null);

/// <summary>Sezione canonica (obbligatoria) di una vLOA, con blocchi e figli (per i Coordinamenti).</summary>
public sealed record VloaSectionSpec(string SectionKey, string Title,
    IReadOnlyList<VloaBlockSpec> Blocks, IReadOnlyList<VloaSectionSpec> Children);

/// <summary>
/// <b>Contenuto</b> iniziale della vLOA (mockup 3d «vLOA Estere»), parametrico su Home/Foreign. La <b>struttura</b>
/// — quali sezioni, con che chiave, che titolo e in che ordine — la dice il <see cref="SectionCatalog"/>, profilo
/// <see cref="SectionProfile.Vloa"/>, come per la vIPI ACC e l'APP (doc 13 §3c).
/// <para>
/// Fino al doc 13 questo file era un <b>registro parallelo</b> espresso nell'enum legacy <c>BlockSection</c>, e il
/// catalogo per la vLOA non lo consultava nessuno: le due descrizioni erano divergenti su tre punti (una sezione in
/// più — «Purpose» —, un ordine diverso e il titolo delle aree regolamentate), e la sola vLOA identificava le sezioni
/// obbligatorie per <b>titolo</b> invece che per chiave.
/// </para>
/// Il seeding delle entità EF resta in Infrastructure (<c>VloaStructureSeeder</c>).
/// </summary>
public static class VloaSections
{
    /// <summary>Chiave della sotto-sezione «direzione» dei coordinamenti (una per verso).</summary>
    private const string CoordinationKey = "coordination";

    /// <summary>Struttura canonica parametrizzata sui codici ACC della coppia (contenuto EN placeholder).</summary>
    public static IReadOnlyList<VloaSectionSpec> Canonical(string homeCode, string foreignCode, string? foreignName, string airacCycle)
    {
        var home = (homeCode ?? "").Trim().ToUpperInvariant();
        var foreign = (foreignCode ?? "").Trim().ToUpperInvariant();
        var fName = string.IsNullOrWhiteSpace(foreignName) ? foreign : foreignName.Trim();

        return SectionCatalog.For(SectionProfile.Vloa)
            .OrderBy(d => d.Order)
            .Select(d => new VloaSectionSpec(d.Key, d.Title, BlocksFor(d.Key, home, foreign, fName, airacCycle),
                ChildrenFor(d.Key, home, foreign)))
            .ToList();
    }

    // Contenuto iniziale per chiave di catalogo. Le sezioni DERIVATE (aor/frequencies) portano solo l'introduzione:
    // la tabella la genera il viewer dai dati. «coordination» non ha corpo proprio — le due direzioni sono le sue
    // sotto-sezioni (doc 11 §3f).
    private static IReadOnlyList<VloaBlockSpec> BlocksFor(string key, string home, string foreign, string fName, string airacCycle) => key switch
    {
        "purpose" => new[]
        {
            Prose($"This Letter of Agreement establishes the coordination procedures, transfer of control and transfer of communications between **{home}** and **{foreign} ({fName})** for traffic crossing the common boundary."),
        },
        "aor" => new[]
        {
            Prose($"Both areas of responsibility are imported from the IVAO database; the common boundary is the {home}/{foreign} ACC limit."),
            Prose($"**{home}:** sectors bordering {foreign}. **{foreign} ({fName}):** sectors bordering {home}."),
        },
        "frequencies" => new[]
        {
            Prose($"Working frequencies of **{home}** and **{foreign} ({fName})** for the sectors along the common boundary (derived from the IVAO database)."),
        },
        "operationaltechnique" => new[]
        {
            Prose("Transfer of control takes place at the common boundary unless otherwise agreed. Transfer of communications is initiated **not later than 5 minutes** before the Coordination Point."),
            Callout(CalloutKind.Warning, "Reduced coordination", "In case of radar/communication degradation, revert to estimates and verbal handoff at the boundary."),
        },
        "regulated" => new[]
        {
            Prose("Activation and crossing of cross-border military areas adjacent to the common boundary are coordinated between the two units."),
        },
        "validity" => new[]
        {
            Table(new[] { "Item", "Value" },
                Cells("Effective from", $"AIRAC {airacCycle}"),
                Cells("Review cycle", "Bilateral, at least annually"),
                Cells("Italian signatory", $"{home} CH / AOD")),
        },
        _ => Array.Empty<VloaBlockSpec>(),
    };

    // Le due direzioni dei coordinamenti. Nessun blocco: il corpo lo produce l'editor (tabella dei trasferimenti)
    // e il viewer le rende dal padre — un paragrafo scritto qui non lo vedrebbe nessuno.
    private static IReadOnlyList<VloaSectionSpec> ChildrenFor(string key, string home, string foreign) =>
        key == CoordinationKey
            ? new[]
            {
                Sec(CoordinationKey, $"{home} → {foreign}"),
                Sec(CoordinationKey, $"{foreign} → {home}"),
            }
            : Array.Empty<VloaSectionSpec>();

    // ---- costruttori spec ----
    private static VloaSectionSpec Sec(string key, string title) =>
        new(key, title, Array.Empty<VloaBlockSpec>(), Array.Empty<VloaSectionSpec>());

    private static VloaBlockSpec Prose(string markdown) => new(BlockFormat.Prose, Body: markdown);

    private static VloaBlockSpec Callout(CalloutKind kind, string title, string markdown) =>
        new(BlockFormat.Callout, Body: markdown, BodyJson: JsonSerializer.Serialize(new { title }), CalloutKind: kind);

    private static VloaBlockSpec Table(string[] columns, params object[] rows) =>
        new(BlockFormat.Table, BodyJson: JsonSerializer.Serialize(new { columns, unified = false, rows }));

    private static object Cells(params string[] cells) => new { cells };
}
