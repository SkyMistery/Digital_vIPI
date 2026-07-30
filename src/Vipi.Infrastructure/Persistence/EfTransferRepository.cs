using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="ITransferRepository"/>: flussi (settore proprio) e loro punti (CoP/livello/Next).</summary>
public sealed class EfTransferRepository : ITransferRepository
{
    private readonly VipiDbContext _db;
    public EfTransferRepository(VipiDbContext db) => _db = db;

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

    public async Task<int> AddFlowAsync(string accCode, TransferFlowInput input, CancellationToken ct = default)
    {
        var accId = await AccIdAsync(accCode, ct);
        var nextOrder = (await _db.TransferFlows
            .Where(f => f.AccId == accId && f.OwningSectorId == input.OwningSectorId)
            .MaxAsync(f => (int?)f.Order, ct) ?? 0) + 1;

        var f = new TransferFlow { AccId = accId, Order = nextOrder };
        ApplyFlow(f, input);
        _db.TransferFlows.Add(f);
        await _db.SaveChangesAsync(ct);
        return f.Id;
    }

    public async Task UpdateFlowAsync(string accCode, int flowId, TransferFlowInput input, CancellationToken ct = default)
    {
        var f = await _db.TransferFlows.FirstOrDefaultAsync(x => x.Id == flowId && x.Acc!.Code == accCode, ct)
            ?? throw new InvalidOperationException($"Flusso {flowId} non appartiene alla ACC {accCode}.");
        ApplyFlow(f, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteFlowAsync(string accCode, int flowId, CancellationToken ct = default)
    {
        var f = await _db.TransferFlows.FirstOrDefaultAsync(x => x.Id == flowId && x.Acc!.Code == accCode, ct);
        if (f is null) return;
        _db.TransferFlows.Remove(f);   // i punti seguono in cascade
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AddPointAsync(string accCode, int flowId, TransferPointInput input, CancellationToken ct = default)
    {
        var flow = await _db.TransferFlows.FirstOrDefaultAsync(x => x.Id == flowId && x.Acc!.Code == accCode, ct)
            ?? throw new InvalidOperationException($"Flusso {flowId} non appartiene alla ACC {accCode}.");
        var nextOrder = (await _db.TransferPoints.Where(p => p.FlowId == flowId).MaxAsync(p => (int?)p.Order, ct) ?? 0) + 1;

        var p = new TransferPoint { FlowId = flow.Id, Order = nextOrder };
        ApplyPoint(p, input);
        _db.TransferPoints.Add(p);
        await _db.SaveChangesAsync(ct);
        return p.Id;
    }

    public async Task UpdatePointAsync(string accCode, int pointId, TransferPointInput input, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        ApplyPoint(p, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeletePointAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        var p = await _db.TransferPoints.FirstOrDefaultAsync(x => x.Id == pointId && x.Flow!.Acc!.Code == accCode, ct);
        if (p is null) return;
        _db.TransferPoints.Remove(p);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MovePointAsync(string accCode, int pointId, bool up, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        // Vicino nello stesso flusso: Order massimo < corrente (su) o minimo > corrente (giù).
        var neighbour = up
            ? await _db.TransferPoints.Where(x => x.FlowId == p.FlowId && x.Order < p.Order)
                .OrderByDescending(x => x.Order).FirstOrDefaultAsync(ct)
            : await _db.TransferPoints.Where(x => x.FlowId == p.FlowId && x.Order > p.Order)
                .OrderBy(x => x.Order).FirstOrDefaultAsync(ct);
        if (neighbour is null) return;   // estremo: no-op
        (p.Order, neighbour.Order) = (neighbour.Order, p.Order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MovePointToEndAsync(string accCode, int pointId, bool top, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        // Fratelli dello stesso flusso in ordine corrente; sposto p all'estremo e ricompatto gli Order (1..N).
        var siblings = await _db.TransferPoints.Where(x => x.FlowId == p.FlowId).OrderBy(x => x.Order).ToListAsync(ct);
        if (siblings.Count < 2) return;
        siblings.Remove(p);
        if (top) siblings.Insert(0, p); else siblings.Add(p);
        for (var i = 0; i < siblings.Count; i++) siblings[i].Order = i + 1;
        await _db.SaveChangesAsync(ct);
    }

    // ---- helper ----

    private async Task<int> AccIdAsync(string accCode, CancellationToken ct) =>
        await _db.Accs.Where(f => f.Code == accCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");

    private async Task<TransferPoint> PointInAccAsync(string accCode, int pointId, CancellationToken ct) =>
        await _db.TransferPoints.FirstOrDefaultAsync(x => x.Id == pointId && x.Flow!.Acc!.Code == accCode, ct)
            ?? throw new InvalidOperationException($"Punto {pointId} non appartiene alla ACC {accCode}.");

    private static void ApplyFlow(TransferFlow f, TransferFlowInput i)
    {
        f.OwningSectorId = i.OwningSectorId;
        f.Kind = i.Kind;
        f.AirportIcao = string.IsNullOrWhiteSpace(i.AirportIcao) ? null : i.AirportIcao.Trim().ToUpperInvariant();
        // Nome solo per aeroporti fuori DB: senza ICAO non ha senso; se c'è, si tiene la stringa grezza.
        f.AirportName = string.IsNullOrWhiteSpace(i.AirportIcao) || string.IsNullOrWhiteSpace(i.AirportName)
            ? null : i.AirportName.Trim();
        f.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
    }

    private static void ApplyPoint(TransferPoint p, TransferPointInput i)
    {
        p.Cop = (i.Cop ?? "").Trim();
        p.LevelValue = i.LevelConstraint == LevelConstraint.Special ? null : i.LevelValue;
        p.LevelUnit = i.LevelUnit;
        p.LevelConstraint = i.LevelConstraint;
        p.LevelSpecial = i.LevelConstraint == LevelConstraint.Special
            ? (string.IsNullOrWhiteSpace(i.LevelSpecial) ? null : i.LevelSpecial.Trim()) : null;
        p.Parity = i.Parity;
        p.VerticalState = i.VerticalState;
        p.NextSectorId = i.NextSectorId;

        // Condizione: tre dimensioni indipendenti (pista/area/personalizzata), ognuna trim→null se vuota.
        // Il soft-ref pista è tenuto solo se c'è una pista.
        p.ConditionLabel = NullIfBlank(i.ConditionLabel);
        p.ConditionRefId = p.ConditionLabel is null ? null : i.ConditionRefId;
        p.ConditionAreaLabel = NullIfBlank(i.ConditionAreaLabel);
        p.ConditionCustomLabel = NullIfBlank(i.ConditionCustomLabel);
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

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
        LevelText = LevelFormatting.Format(p.LevelValue, p.LevelUnit, p.LevelConstraint, p.LevelSpecial, p.Parity, p.VerticalState),
        NextSectorId = p.NextSectorId,
        NextSectorCallsign = p.NextSector?.Callsign,
        ConditionLabel = p.ConditionLabel,
        ConditionRefId = p.ConditionRefId,
        ConditionAreaLabel = p.ConditionAreaLabel,
        ConditionCustomLabel = p.ConditionCustomLabel,
        Order = p.Order,
    };
}
