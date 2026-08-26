using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Ponte tra l'enum legacy <see cref="BlockSection"/> del modello classic e le chiavi del
/// <see cref="SectionCatalog"/> unificato (doc refactor 08c). Ogni migrazione per-tipo (08d) lo usa per
/// tradurre le sezioni esistenti nelle chiavi del catalogo. Additivo: non modifica il modello attuale.
/// </summary>
public static class SectionCatalogBridge
{
    /// <summary>
    /// Chiave del catalogo corrispondente a una <see cref="BlockSection"/>, o <c>null</c> se non ha una
    /// corrispondenza fissa (→ sezione editoriale generica).
    /// <para>
    /// ⚠️ <see cref="BlockSection.Airport"/> resta senza chiave, ed è la ragione per cui le sezioni
    /// d'aeroporto cotte fino alla carta 2026-08-26 ne prendevano una CASUALE: il builder ricadeva su
    /// <c>SectionKeys.NewCustom()</c>. Da quella carta l'aeroporto ha un profilo suo nel catalogo e non passa
    /// più di qui — questo enum descrive solo il modello classic.
    /// </para>
    /// </summary>
    public static string? KeyFor(BlockSection section) => section switch
    {
        BlockSection.Aor => "aor",
        BlockSection.Frequencies => "frequencies",
        BlockSection.Coordination => "coordination",
        BlockSection.OperationalTechnique => "operationaltechnique",
        BlockSection.Separations => "separations",
        BlockSection.AreasCorridors => "regulated",   // fusa: Military areas → Aree regolamentate
        BlockSection.Validity => "validity",
        // Airport (ambiguo, 5 sezioni), Purpose (rimossa), OperationalSettings/Atis/TrafficManagement/
        // BestPractice/Other → editoriale/custom, risolti nella migrazione per-tipo.
        _ => null,
    };

    /// <summary>Vero se la sezione ha una chiave fissa nel catalogo (mappatura 1:1).</summary>
    public static bool HasCatalogKey(BlockSection section) => KeyFor(section) is not null;
}
