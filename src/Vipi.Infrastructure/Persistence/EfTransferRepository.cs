using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="ITransferRepository"/>. La catena handler è un array JSON ordinato.</summary>
public sealed class EfTransferRepository : ITransferRepository
{
    private readonly VipiDbContext _db;
    public EfTransferRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<TransferRow>> ListByFirAsync(string firCode, CancellationToken ct = default)
    {
        var rows = await _db.Transfers
            .Where(t => t.Fir!.Code == firCode)
            .OrderBy(t => t.RelationKey).ThenBy(t => t.Phase).ThenBy(t => t.Order)
            .AsNoTracking().ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<int> AddAsync(string firCode, TransferInput input, CancellationToken ct = default)
    {
        var firId = await _db.Firs.Where(f => f.Code == firCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"FIR {firCode} inesistente.");

        var nextOrder = (await _db.Transfers
            .Where(t => t.FirId == firId && t.RelationKey == input.RelationKey && t.Phase == input.Phase)
            .MaxAsync(t => (int?)t.Order, ct) ?? 0) + 1;

        var t = new Transfer { FirId = firId, Order = nextOrder };
        Apply(t, input);
        _db.Transfers.Add(t);
        await _db.SaveChangesAsync(ct);
        return t.Id;
    }

    public async Task UpdateAsync(string firCode, int id, TransferInput input, CancellationToken ct = default)
    {
        var t = await InFirAsync(firCode, id, ct);
        Apply(t, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string firCode, int id, CancellationToken ct = default)
    {
        var t = await _db.Transfers.FirstOrDefaultAsync(x => x.Id == id && x.Fir!.Code == firCode, ct);
        if (t is null) return;
        _db.Transfers.Remove(t);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Transfer> InFirAsync(string firCode, int id, CancellationToken ct) =>
        await _db.Transfers.FirstOrDefaultAsync(x => x.Id == id && x.Fir!.Code == firCode, ct)
            ?? throw new InvalidOperationException($"Trasferimento {id} non appartiene alla FIR {firCode}.");

    private static void Apply(Transfer t, TransferInput i)
    {
        t.RelationKey = i.RelationKey;
        t.RelationLabel = i.RelationLabel;
        t.Phase = i.Phase;
        t.AirportIcao = i.AirportIcao;
        t.Cop = i.Cop;
        t.FlRule = i.FlRule;
        t.HandlerChainJson = JsonSerializer.Serialize(i.HandlerChain);
        t.StandardFallback = string.IsNullOrWhiteSpace(i.StandardFallback) ? "UNICOM" : i.StandardFallback;
    }

    private static TransferRow Map(Transfer t) => new()
    {
        Id = t.Id,
        RelationKey = t.RelationKey,
        RelationLabel = t.RelationLabel,
        Phase = t.Phase,
        AirportIcao = t.AirportIcao,
        Cop = t.Cop,
        FlRule = t.FlRule,
        HandlerChain = ParseChain(t.HandlerChainJson),
        StandardFallback = t.StandardFallback,
        Order = t.Order,
    };

    private static IReadOnlyList<string> ParseChain(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch (JsonException) { return new List<string>(); }
    }
}
