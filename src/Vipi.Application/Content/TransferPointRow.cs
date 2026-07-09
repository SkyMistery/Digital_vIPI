using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Riga CoP di un flusso (lettura). <see cref="LevelText"/> è il livello già formattato.</summary>
public sealed class TransferPointRow
{
    public required int Id { get; init; }
    public required string Cop { get; init; }
    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public required string LevelText { get; init; }
    public int? NextSectorId { get; init; }
    public string? NextSectorCallsign { get; init; }
    public required int Order { get; init; }
}
