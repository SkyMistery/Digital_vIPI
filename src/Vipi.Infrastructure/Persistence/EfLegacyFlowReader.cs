using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="ILegacyFlowReader"/>
public sealed class EfLegacyFlowReader : ILegacyFlowReader
{
    private readonly VipiDbContext _db;
    public EfLegacyFlowReader(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<TransferFlowRow>> ListFlowsByAccAsync(string accCode, CancellationToken ct = default)
    {
        var flows = await _db.TransferFlows.AsNoTracking()
            .Where(f => f.Acc!.Code == accCode)
            .Include(f => f.OwningSector)
            .Include(f => f.Points).ThenInclude(p => p.NextSector)
            .OrderBy(f => f.OwningSectorId).ThenBy(f => f.Order)
            .ToListAsync(ct);

        return flows.Select(MapFlow).ToList();
    }

    private static TransferFlowRow MapFlow(TransferFlow f) => new()
    {
        Id = f.Id,
        AccCode = f.Acc?.Code ?? "",
        OwningSectorId = f.OwningSectorId,
        OwningSectorCallsign = f.OwningSector?.Callsign ?? $"#{f.OwningSectorId}",
        Kind = f.Kind,
        AirportIcao = f.AirportIcao,
        AirportName = f.AirportName,
        Description = f.Description,
        Order = f.Order,
        Points = f.Points.OrderBy(p => p.Order).Select(MapPoint).ToList(),
    };

    private static TransferPointRow MapPoint(TransferPoint p) => new()
    {
        Id = p.Id,
        Cop = p.Cop,
        LevelValue = p.LevelValue,
        LevelUnit = p.LevelUnit,
        LevelConstraint = p.LevelConstraint,
        LevelSpecial = p.LevelSpecial,
        Parity = p.Parity,
        VerticalState = p.VerticalState,
        LevelText = LevelFormatting.Format(p.LevelValue, p.LevelUnit, p.LevelConstraint, p.LevelSpecial,
                                           p.Parity, p.VerticalState),
        NextSectorId = p.NextSectorId,
        NextSectorCallsign = p.NextSector?.Callsign,
        ConditionLabel = p.ConditionLabel,
        ConditionRefId = p.ConditionRefId,
        ConditionAreaLabel = p.ConditionAreaLabel,
        ConditionCustomLabel = p.ConditionCustomLabel,
        HandoffKind = p.HandoffKind,
        HandoffLabel = p.HandoffLabel,
        HandoffLevelValue = p.HandoffLevelValue,
        HandoffLevelUnit = p.HandoffLevelUnit,
        HandoffLevelConstraint = p.HandoffLevelConstraint,
        CommsHandoffKind = p.CommsHandoffKind,
        CommsHandoffLabel = p.CommsHandoffLabel,
        SpeedValue = p.SpeedValue,
        SpeedConstraint = p.SpeedConstraint,
        VariantGroup = p.VariantGroup,
        VariantDepth = p.VariantDepth,
        IsGroupWide = p.IsGroupWide,
        Order = p.Order,
    };
}
