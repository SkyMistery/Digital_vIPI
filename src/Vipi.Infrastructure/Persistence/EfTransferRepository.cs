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

    public async Task<IReadOnlyList<TransferRow>> ListByAccAsync(string accCode, CancellationToken ct = default)
    {
        var rows = await _db.Transfers
            .Where(t => t.Acc!.Code == accCode)
            .OrderBy(t => t.RelationKey).ThenBy(t => t.Phase).ThenBy(t => t.Order)
            .AsNoTracking().ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<int> AddAsync(string accCode, TransferInput input, CancellationToken ct = default)
    {
        var accId = await _db.Accs.Where(f => f.Code == accCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");

        var nextOrder = (await _db.Transfers
            .Where(t => t.AccId == accId && t.RelationKey == input.RelationKey && t.Phase == input.Phase)
            .MaxAsync(t => (int?)t.Order, ct) ?? 0) + 1;

        var t = new Transfer { AccId = accId, Order = nextOrder };
        Apply(t, input);
        _db.Transfers.Add(t);
        await _db.SaveChangesAsync(ct);
        return t.Id;
    }

    public async Task UpdateAsync(string accCode, int id, TransferInput input, CancellationToken ct = default)
    {
        var t = await InAccAsync(accCode, id, ct);
        Apply(t, input);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string accCode, int id, CancellationToken ct = default)
    {
        var t = await _db.Transfers.FirstOrDefaultAsync(x => x.Id == id && x.Acc!.Code == accCode, ct);
        if (t is null) return;
        _db.Transfers.Remove(t);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<Transfer> InAccAsync(string accCode, int id, CancellationToken ct) =>
        await _db.Transfers.FirstOrDefaultAsync(x => x.Id == id && x.Acc!.Code == accCode, ct)
            ?? throw new InvalidOperationException($"Trasferimento {id} non appartiene alla ACC {accCode}.");

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
