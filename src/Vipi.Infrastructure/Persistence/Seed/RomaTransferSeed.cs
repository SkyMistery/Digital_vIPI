using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>Seed demo di trasferimenti strutturati per Roma (relazioni Roma↔Milano e Roma↔Tunisi). Idempotente.</summary>
public static class RomaTransferSeed
{
    public static async Task SeedAsync(VipiDbContext db, CancellationToken ct = default)
    {
        var acc = await db.Accs.FirstOrDefaultAsync(f => f.Code == "LIRR", ct);
        if (acc is null) return;
        if (await db.Transfers.AnyAsync(t => t.AccId == acc.Id, ct)) return;

        var rows = new[]
        {
            T(acc.Id, "LIRR-LIMM", "Roma ↔ Milano", TransferPhase.Arrival, "LIMC", "VALMA", "FL280↑", new[] { "WS2" }, "UNICOM", 1),
            T(acc.Id, "LIRR-LIMM", "Roma ↔ Milano", TransferPhase.Arrival, "LIMC", "DEVOX", "FL250↑", new[] { "ES2", "WS2" }, "UNICOM", 2),
            T(acc.Id, "LIRR-LIMM", "Roma ↔ Milano", TransferPhase.Departure, "LIRF", "TARQ", "FL250↑", new[] { "WS2" }, "UNICOM", 1),
            T(acc.Id, "LIRR-DTTC", "Roma ↔ Tunisi", TransferPhase.Departure, "DTTA", "ESEBA", "FL350↑", new[] { "DTTC" }, "Confine", 1),
        };
        db.Transfers.AddRange(rows);
        await db.SaveChangesAsync(ct);
    }

    private static Transfer T(int accId, string key, string label, TransferPhase phase, string apt,
        string cop, string fl, string[] chain, string fallback, int order) => new()
    {
        AccId = accId, RelationKey = key, RelationLabel = label, Phase = phase, AirportIcao = apt,
        Cop = cop, FlRule = fl, HandlerChainJson = JsonSerializer.Serialize(chain),
        StandardFallback = fallback, Order = order,
    };
}
