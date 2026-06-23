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
        var fir = await db.Firs.FirstOrDefaultAsync(f => f.Code == "LIRR", ct);
        if (fir is null) return;
        if (await db.Transfers.AnyAsync(t => t.FirId == fir.Id, ct)) return;

        var rows = new[]
        {
            T(fir.Id, "LIRR-LIMM", "Roma ↔ Milano", TransferPhase.Arrival, "LIMC", "VALMA", "FL280↑", new[] { "WS2" }, "UNICOM", 1),
            T(fir.Id, "LIRR-LIMM", "Roma ↔ Milano", TransferPhase.Arrival, "LIMC", "DEVOX", "FL250↑", new[] { "ES2", "WS2" }, "UNICOM", 2),
            T(fir.Id, "LIRR-LIMM", "Roma ↔ Milano", TransferPhase.Departure, "LIRF", "TARQ", "FL250↑", new[] { "WS2" }, "UNICOM", 1),
            T(fir.Id, "LIRR-DTTC", "Roma ↔ Tunisi", TransferPhase.Departure, "DTTA", "ESEBA", "FL350↑", new[] { "DTTC" }, "Confine", 1),
        };
        db.Transfers.AddRange(rows);
        await db.SaveChangesAsync(ct);
    }

    private static Transfer T(int firId, string key, string label, TransferPhase phase, string apt,
        string cop, string fl, string[] chain, string fallback, int order) => new()
    {
        FirId = firId, RelationKey = key, RelationLabel = label, Phase = phase, AirportIcao = apt,
        Cop = cop, FlRule = fl, HandlerChainJson = JsonSerializer.Serialize(chain),
        StandardFallback = fallback, Order = order,
    };
}
