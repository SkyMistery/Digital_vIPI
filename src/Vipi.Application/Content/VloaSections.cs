using System.Text.Json;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Blocco di contenuto canonico di una sezione vLOA (spec puro, materializzato lato Infrastructure).</summary>
public sealed record VloaBlockSpec(BlockFormat Format, string? Body = null, string? BodyJson = null, CalloutKind? CalloutKind = null);

/// <summary>Sezione canonica (obbligatoria) di una vLOA, con blocchi e figli (per i Coordinamenti).</summary>
public sealed record VloaSectionSpec(string Title, BlockSection Kind,
    IReadOnlyList<VloaBlockSpec> Blocks, IReadOnlyList<VloaSectionSpec> Children);

/// <summary>
/// Registro della struttura OBBLIGATORIA della vLOA (mockup 3d «vLOA Estere»): stesse sezioni della lettera di
/// accordo bilaterale. Puro/parametrico su Home/Foreign; il seeding delle entità EF è in Infrastructure
/// (<c>VloaStructureSeeder</c>), e l'editor blocca queste sezioni via <see cref="MandatoryTitles"/>.
/// Rispecchia <c>RomaVloaSeed</c> ma generico (nessun testo Roma-specifico).
/// </summary>
public static class VloaSections
{
    /// <summary>Struttura canonica parametrizzata sui codici ACC della coppia (contenuto EN placeholder).</summary>
    public static IReadOnlyList<VloaSectionSpec> Canonical(string homeCode, string foreignCode, string? foreignName, string airacCycle)
    {
        var home = (homeCode ?? "").Trim().ToUpperInvariant();
        var foreign = (foreignCode ?? "").Trim().ToUpperInvariant();
        var fName = string.IsNullOrWhiteSpace(foreignName) ? foreign : foreignName.Trim();

        return new List<VloaSectionSpec>
        {
            Sec("Purpose", BlockSection.Purpose,
                Prose($"This Letter of Agreement establishes the coordination procedures, transfer of control and transfer of communications between **{home}** and **{foreign} ({fName})** for traffic crossing the common boundary.")),

            Sec("Areas of Responsibility", BlockSection.Aor,
                Prose($"Both areas of responsibility are imported from the IVAO database; the common boundary is the {home}/{foreign} ACC limit."),
                Prose($"**{home}:** sectors bordering {foreign}. **{foreign} ({fName}):** sectors bordering {home}.")),

            // Frequenze DERIVATE (data-driven) unendo i due ACC: la tabella è generata dall'editor/viewer, qui solo intro.
            Sec("Frequencies", BlockSection.Frequencies,
                Prose($"Working frequencies of **{home}** and **{foreign} ({fName})** for the sectors along the common boundary (derived from the IVAO database).")),

            Sec("General procedures", BlockSection.OperationalTechnique,
                Prose("Transfer of control takes place at the common boundary unless otherwise agreed. Transfer of communications is initiated **not later than 5 minutes** before the Coordination Point."),
                Callout(CalloutKind.Warning, "Reduced coordination", "In case of radar/communication degradation, revert to estimates and verbal handoff at the boundary.")),

            // Coordinamenti DERIVATI dai trasferimenti registrati (una tabella per direzione, generata dall'editor/viewer).
            SecWithChildren("Coordination", BlockSection.Coordination, Array.Empty<VloaBlockSpec>(),
                Sec($"{home} → {foreign}", BlockSection.Coordination,
                    Prose($"**{home} transfers** traffic to {foreign} at the Coordination Point, as published.")),
                Sec($"{foreign} → {home}", BlockSection.Coordination,
                    Prose($"**{foreign} transfers** traffic to {home} at the Coordination Point, as published."))),

            Sec("Military areas coordination and management", BlockSection.AreasCorridors,
                Prose("Activation and crossing of cross-border military areas adjacent to the common boundary are coordinated between the two units.")),

            Sec("Validity and Revision", BlockSection.Validity,
                Table(new[] { "Item", "Value" },
                    Cells("Effective from", $"AIRAC {airacCycle}"),
                    Cells("Review cycle", "Bilateral, at least annually"),
                    Cells("Italian signatory", $"{home} CH / AOD"))),
        };
    }

    /// <summary>Titoli delle sezioni obbligatorie (7 + 2 figli Coordinamenti), per il lock nell'editor.</summary>
    public static IReadOnlySet<string> MandatoryTitles(string homeCode, string foreignCode)
    {
        var home = (homeCode ?? "").Trim().ToUpperInvariant();
        var foreign = (foreignCode ?? "").Trim().ToUpperInvariant();
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Purpose", "Areas of Responsibility", "Frequencies", "General procedures",
            "Coordination", $"{home} → {foreign}", $"{foreign} → {home}",
            "Military areas coordination and management", "Validity and Revision",
        };
    }

    // ---- costruttori spec ----
    private static VloaSectionSpec Sec(string title, BlockSection kind, params VloaBlockSpec[] blocks) =>
        new(title, kind, blocks, Array.Empty<VloaSectionSpec>());

    private static VloaSectionSpec SecWithChildren(string title, BlockSection kind,
        IReadOnlyList<VloaBlockSpec> blocks, params VloaSectionSpec[] children) =>
        new(title, kind, blocks, children);

    private static VloaBlockSpec Prose(string markdown) => new(BlockFormat.Prose, Body: markdown);

    private static VloaBlockSpec Callout(CalloutKind kind, string title, string markdown) =>
        new(BlockFormat.Callout, Body: markdown, BodyJson: JsonSerializer.Serialize(new { title }), CalloutKind: kind);

    private static VloaBlockSpec Table(string[] columns, params object[] rows) =>
        new(BlockFormat.Table, BodyJson: JsonSerializer.Serialize(new { columns, unified = false, rows }));

    private static object Cells(params string[] cells) => new { cells };
}
