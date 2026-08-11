using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;      // ValidationException: la UI cattura questa, mai quella di DataAnnotations
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

        if (p.VariantGroup is int group)
        {
            var siblings = await GroupSiblingsAsync(p, group, ct);

            // «Negli altri casi» è il complemento delle sorelle: una sola per gruppo, altrimenti il lettore ha
            // due catch-all e nessuna regola per sceglierne uno.
            if (p.IsOtherwise && siblings.Any(s => s.IsOtherwise))
                throw new ValidationException("Il gruppo di varianti ha già una riga «negli altri casi».");

            // CoP e ricevente sono l'IDENTITÀ dell'accordo, condivisa da tutte le varianti: cambiarli su una riga
            // li cambia sul gruppo. Propagare è meglio che rifiutare — l'invariante resta vera senza chiedere
            // all'editore di ripetere la stessa modifica su ogni riga.
            foreach (var s in siblings)
            {
                s.Cop = p.Cop;
                s.NextSectorId = p.NextSectorId;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeletePointAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        var p = await _db.TransferPoints.FirstOrDefaultAsync(x => x.Id == pointId && x.Flow!.Acc!.Code == accCode, ct);
        if (p is null) return;
        var group = p.VariantGroup;
        _db.TransferPoints.Remove(p);
        if (group is int g) await DissolveIfAloneAsync(p.FlowId, g, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AddVariantAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        var src = await PointInAccAsync(accCode, pointId, ct);

        // Il gruppo nasce alla prima variante: progressivo per flusso, indipendente dagli Id (leggibile e stabile).
        if (src.VariantGroup is null)
            src.VariantGroup = (await _db.TransferPoints.Where(x => x.FlowId == src.FlowId)
                .MaxAsync(x => (int?)x.VariantGroup, ct) ?? 0) + 1;

        // La variante è una COPIA COMPLETA meno la condizione: i dati restano piatti (nessuna eredità di campo,
        // che con LevelValue nullable sarebbe ambigua), ma chi scrive non ridigita dieci campi per cambiarne uno.
        // La condizione resta vuota perché è esattamente ciò che la variante deve dire di diverso.
        var copy = new TransferPoint
        {
            FlowId = src.FlowId,
            Cop = src.Cop,
            LevelValue = src.LevelValue,
            LevelUnit = src.LevelUnit,
            LevelConstraint = src.LevelConstraint,
            LevelSpecial = src.LevelSpecial,
            Parity = src.Parity,
            VerticalState = src.VerticalState,
            NextSectorId = src.NextSectorId,
            HandoffKind = src.HandoffKind,
            HandoffLabel = src.HandoffLabel,
            HandoffLevelValue = src.HandoffLevelValue,
            HandoffLevelUnit = src.HandoffLevelUnit,
            HandoffLevelConstraint = src.HandoffLevelConstraint,
            CommsHandoffKind = src.CommsHandoffKind,
            CommsHandoffLabel = src.CommsHandoffLabel,
            SpeedValue = src.SpeedValue,
            SpeedConstraint = src.SpeedConstraint,
            VariantGroup = src.VariantGroup,
            Order = src.Order + 1,
        };

        // Spazio subito sotto la riga sorgente: le varianti stanno vicine, che è come si leggono.
        var below = await _db.TransferPoints.Where(x => x.FlowId == src.FlowId && x.Order > src.Order).ToListAsync(ct);
        foreach (var x in below) x.Order++;

        _db.TransferPoints.Add(copy);
        await _db.SaveChangesAsync(ct);
        return copy.Id;
    }

    public async Task DetachVariantAsync(string accCode, int pointId, CancellationToken ct = default)
    {
        var p = await PointInAccAsync(accCode, pointId, ct);
        if (p.VariantGroup is not int group) return;
        p.VariantGroup = null;
        p.IsOtherwise = false;
        await DissolveIfAloneAsync(p.FlowId, group, ct);
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

    /// <summary>Le altre righe dello stesso gruppo di varianti (la riga passata esclusa).</summary>
    private Task<List<TransferPoint>> GroupSiblingsAsync(TransferPoint p, int group, CancellationToken ct) =>
        _db.TransferPoints.Where(x => x.FlowId == p.FlowId && x.VariantGroup == group && x.Id != p.Id).ToListAsync(ct);

    /// <summary>Scioglie un gruppo rimasto con una sola riga: un gruppo di uno non è un gruppo, e lasciarlo
    /// significherebbe rendere una riga singola con l'intestazione di gruppo e un «negli altri casi» senza «casi».
    /// Non salva: il chiamante è già dentro una sua <c>SaveChangesAsync</c>.</summary>
    private async Task DissolveIfAloneAsync(int flowId, int group, CancellationToken ct)
    {
        var candidati = await _db.TransferPoints
            .Where(x => x.FlowId == flowId && x.VariantGroup == group)
            .ToListAsync(ct);

        // La query filtra su ciò che sta NEL DATABASE, ma qui siamo prima della SaveChanges: la riga appena
        // sfilata (VariantGroup = null) o appena rimossa torna comunque indietro dal SELECT. Va riletto lo
        // stato in memoria, che è quello che sta per essere scritto — altrimenti il gruppo sembra ancora
        // affollato e non si scioglie mai.
        var remaining = candidati
            .Where(x => x.VariantGroup == group && _db.Entry(x).State != EntityState.Deleted)
            .ToList();
        if (remaining.Count > 1) return;
        foreach (var x in remaining) { x.VariantGroup = null; x.IsOtherwise = false; }
    }

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

        // Faccetta trasferimento. Senza tipo non c'è trasferimento distinto: i campi correlati vengono azzerati,
        // così una riga tornata a «coincide con l'ingresso» non si porta dietro un livello fantasma.
        p.HandoffKind = i.HandoffKind;
        p.HandoffLabel = i.HandoffKind == TransferHandoffKind.Unspecified ? null : NullIfBlank(i.HandoffLabel);
        p.HandoffLevelValue = i.HandoffKind == TransferHandoffKind.Unspecified ? null : i.HandoffLevelValue;
        p.HandoffLevelUnit = i.HandoffLevelUnit;
        p.HandoffLevelConstraint = i.HandoffLevelConstraint;
        p.CommsHandoffKind = i.CommsHandoffKind;
        p.CommsHandoffLabel = i.CommsHandoffKind == TransferHandoffKind.Unspecified ? null : NullIfBlank(i.CommsHandoffLabel);

        // Velocità: senza vincolo non c'è restrizione, e il valore residuo sparisce con essa.
        p.SpeedConstraint = i.SpeedConstraint;
        p.SpeedValue = i.SpeedConstraint == SpeedConstraint.Unspecified ? null : i.SpeedValue;

        // «Negli altri casi» ha senso solo dentro un gruppo, e il gruppo lo assegna AddVariantAsync: qui il flag
        // si accetta solo se la riga è già in un gruppo (VariantGroup non è un campo dell'input, apposta).
        p.IsOtherwise = i.IsOtherwise && p.VariantGroup is not null;
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
        IsOtherwise = p.IsOtherwise,
        Order = p.Order,
    };
}
