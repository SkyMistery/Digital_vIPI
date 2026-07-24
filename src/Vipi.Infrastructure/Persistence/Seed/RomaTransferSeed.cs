using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>Seed demo dei coordinamenti di Roma: flussi del settore NE/EW coi loro punti (CoP/livello/Next). Idempotente.</summary>
public static class RomaTransferSeed
{
    public static async Task SeedAsync(VipiDbContext db, CancellationToken ct = default)
    {
        var acc = await db.Accs.FirstOrDefaultAsync(f => f.Code == "LIRR", ct);
        if (acc is null) return;
        if (await db.TransferFlows.AnyAsync(f => f.AccId == acc.Id, ct)) return;

        var byCs = await db.Sectors.Where(s => s.AccId == acc.Id)
            .ToDictionaryAsync(s => s.Callsign, s => s.Id, StringComparer.OrdinalIgnoreCase, ct);
        int? Id(string cs) => byCs.TryGetValue(cs, out var v) ? v : null;
        var ne = Id("LIRR_NE_CTR"); var ew = Id("LIRR_EW_CTR");
        if (ne is null || ew is null) return;

        // NE · Traffico Dest LIRF — arrivi in discesa agli avvicinamenti/torre.
        var f1 = new TransferFlow
        {
            AccId = acc.Id, OwningSectorId = ne.Value, Kind = TransferFlowKind.Arrival, AirportIcao = "LIRF", Order = 1,
            Description = "Roma NE trasferisce il traffico in arrivo su LIRF in discesa per i CoP pubblicati.",
            Points =
            {
                P("VALMA", 130, LevelConstraint.AtOrBelow, Id("LIRF_TWR"), 1),
                P("ELKAP", 150, LevelConstraint.AtOrBelow, Id("LIRF_TWR"), 2),
            },
        };
        // NE · Traffico DEP LIRF — partenze in salita verso i confinanti.
        var f2 = new TransferFlow
        {
            AccId = acc.Id, OwningSectorId = ne.Value, Kind = TransferFlowKind.Departure, AirportIcao = "LIRF", Order = 2,
            Description = "Roma NE riceve le partenze da LIRF in salita e le instrada ai settori confinanti.",
            Points = { P("TARQ", 280, LevelConstraint.AtOrAbove, Id("LIRR_TS_CTR"), 1) },
        };
        // EW · Sorvoli senza aeroporto — ceduti al settore confinante per regola semicircolare (dispari/pari).
        var f3 = new TransferFlow
        {
            AccId = acc.Id, OwningSectorId = ew.Value, Kind = TransferFlowKind.Overflight, Order = 1,
            Description = "Sorvoli mantenuti al livello di aerovia e ceduti a Roma TS: dispari/pari per direzione.",
            Points =
            {
                P("ELB", 350, LevelConstraint.AtOrAbove, Id("LIRR_TS_CTR"), 1, LevelParity.Odd),
                P("ELB", 360, LevelConstraint.AtOrAbove, Id("LIRR_TS_CTR"), 2, LevelParity.Even),
            },
        };

        db.TransferFlows.AddRange(f1, f2, f3);
        await db.SaveChangesAsync(ct);
    }

    private static TransferPoint P(string cop, int level, LevelConstraint constraint, int? nextSectorId, int order,
        LevelParity parity = LevelParity.Any) => new()
    {
        Cop = cop, LevelValue = level, LevelUnit = LevelUnit.Fl, LevelConstraint = constraint,
        Parity = parity, VerticalState = VStateFrom(constraint), NextSectorId = nextSectorId, Order = order,
    };

    // Stato verticale di demo derivato dal vincolo (come il backfill della migrazione): mantiene le frasi
    // «in discesa/salita/stabile» sui dati di esempio. In produzione lo stato è un campo indipendente scelto a mano.
    private static TransferVerticalState VStateFrom(LevelConstraint c) => c switch
    {
        LevelConstraint.AtOrBelow => TransferVerticalState.Descending,
        LevelConstraint.AtOrAbove => TransferVerticalState.Climbing,
        LevelConstraint.Exact => TransferVerticalState.Level,
        _ => TransferVerticalState.Unspecified,
    };
}
