namespace Vipi.Application.Aor;

/// <summary>Settore (id + callsign) per i picker dell'editor topologia.</summary>
public sealed record SectorRef(int Id, string Callsign);

/// <summary>Regola di unificazione come riga editabile.</summary>
public sealed class RuleRow
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int Priority { get; init; }
    public required string ConditionJson { get; init; }
    public required string AssignmentJson { get; init; }
    public required bool IsActive { get; init; }
}

/// <summary>Contenimento padre→figlio come riga (derivata da Sector.ParentSectorId). Id = id del settore figlio.</summary>
public sealed class HierarchyRow
{
    public required int ChildSectorId { get; init; }
    public required string ChildCallsign { get; init; }
    public required int ParentSectorId { get; init; }
    public required string ParentCallsign { get; init; }
}

/// <summary>Dati di editing della topologia di una FIR (anagrafica in sola lettura, regole/contenimento editabili).</summary>
public sealed class TopologyEditData
{
    public required int FirId { get; init; }
    public required IReadOnlyList<SectorRef> Sectors { get; init; }
    public required IReadOnlyList<RuleRow> Rules { get; init; }
    public required IReadOnlyList<HierarchyRow> Hierarchy { get; init; }
}
