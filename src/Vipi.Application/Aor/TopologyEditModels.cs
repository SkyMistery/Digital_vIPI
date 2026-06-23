namespace Vipi.Application.Aor;

/// <summary>Posizione (id + callsign) per i picker dell'editor topologia.</summary>
public sealed record PositionRef(int Id, string Callsign);

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

/// <summary>Relazione gerarchica padre→figlio come riga editabile.</summary>
public sealed class HierarchyRow
{
    public required int Id { get; init; }
    public required int ParentPositionId { get; init; }
    public required string ParentCallsign { get; init; }
    public required int ChildPositionId { get; init; }
    public required string ChildCallsign { get; init; }
}

/// <summary>Dati di editing della topologia di una FIR (anagrafica in sola lettura, regole/gerarchia editabili).</summary>
public sealed class TopologyEditData
{
    public required int FirId { get; init; }
    public required IReadOnlyList<PositionRef> Positions { get; init; }
    public required IReadOnlyList<RuleRow> Rules { get; init; }
    public required IReadOnlyList<HierarchyRow> Hierarchy { get; init; }
}
