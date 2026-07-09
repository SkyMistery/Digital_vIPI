using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Dati per creare/aggiornare un punto di un flusso.</summary>
public sealed class TransferPointInput
{
    public required string Cop { get; init; }
    public int? LevelValue { get; init; }
    public required LevelUnit LevelUnit { get; init; }
    public required LevelConstraint LevelConstraint { get; init; }
    public string? LevelSpecial { get; init; }
    public int? NextSectorId { get; init; }
}
