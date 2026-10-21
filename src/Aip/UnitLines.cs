namespace Aip;

/// <summary>Un punto di una <see cref="AtsUnitLine"/>: col nome (<c>LUSIL</c>, la posizione viene da ENR 4.4) o con le coordinate.</summary>
public sealed record LinePoint
{
    public string? Designator { get; init; }

    public Position? Position { get; init; }
}

/// <summary>
/// Una linea che divide le aree di competenza degli ACC/FIC, dalle note della FIR (ENR 2.1.1.1-3; carta «L'AIP intero»,
/// passo 2c): «Milano ACC/FIC: a nord-ovest della linea congiungente i punti 461715N 0100029E – LUSIL – … nello spazio
/// aereo compreso tra FL195 (escluso) e UNL». Una unità ha più linee, una per fascia di quota o per lato.
/// </summary>
public sealed record AtsUnitLine
{
    public required string Section { get; init; }

    /// <summary>L'unità com'è stampata: <c>Milano ACC/FIC</c>.</summary>
    public required string Unit { get; init; }

    /// <summary>Da che parte della linea sta l'area dell'unità, com'è stampato: <c>nord-ovest</c>, <c>est</c>, <c>sud</c>.</summary>
    public required string Side { get; init; }

    public IReadOnlyList<LinePoint> Points { get; init; } = [];

    /// <summary>Il limite inferiore e quello superiore, se la frase li dice (<c>SFC</c> · <c>FL 195 (incluso)</c>).</summary>
    public VerticalLimit? Lower { get; init; }

    public VerticalLimit? Upper { get; init; }

    /// <summary>La frase com'è stampata.</summary>
    public required string Text { get; init; }

    public AipPage? Page { get; init; }

    public bool Equals(AtsUnitLine? other) =>
        other is not null && Section == other.Section && Unit == other.Unit && Side == other.Side
        && Points.SequenceEqual(other.Points) && Lower == other.Lower && Upper == other.Upper && Text == other.Text && Page == other.Page;

    public override int GetHashCode() => HashCode.Combine(Section, Unit, Side, Points.Count);
}
