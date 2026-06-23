using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed strutturale (no contenuti) della FIR pilota Roma (LIRR): anagrafica + gerarchia top-down +
/// ownership settori + regole di unificazione. PIANO §17.1. Idempotente: no-op se LIRR esiste già.
/// In produzione questa anagrafica arriverà dalle API IVAO; la gerarchia/regole restano dato manuale.
/// </summary>
public static class RomaStructureSeed
{
    public const string FirCode = "LIRR";

    public static async Task<int> SeedAsync(VipiDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Firs.FirstOrDefaultAsync(f => f.Code == FirCode, ct);
        if (existing is not null) return existing.Id;

        var fir = new Fir { Code = FirCode, Name = "Roma FIR", CountryPrefix = "LI" };
        db.Firs.Add(fir);
        await db.SaveChangesAsync(ct); // serve l'Id per le FK seguenti

        // --- Settori ---
        var sectors = new[]
        {
            S(fir.Id, "NE", "Roma NE"), S(fir.Id, "EW", "Roma EW"),
            S(fir.Id, "SU", "Roma SU"), S(fir.Id, "ES", "Roma ES"),
            S(fir.Id, "TS", "Roma TS"),
            S(fir.Id, "PISA", "Pisa TMA"), S(fir.Id, "PISA_TWR", "Pisa TWR"),
        };
        db.Sectors.AddRange(sectors);

        // --- Posizioni ---
        var ne  = P(fir.Id, "LIRR_NE_CTR", PositionType.Ctr, PositionKind.Acc, "Roma Radar NE", "128.800", 10);
        var ew  = P(fir.Id, "LIRR_EW_CTR", PositionType.Ctr, PositionKind.Acc, "Roma Radar EW", "133.250", 10);
        var su  = P(fir.Id, "LIRR_SU_CTR", PositionType.Ctr, PositionKind.Acc, "Roma Radar SU", "125.100", 10);
        var es  = P(fir.Id, "LIRR_ES_CTR", PositionType.Ctr, PositionKind.Acc, "Roma Radar ES", "129.350", 11);
        var ts  = P(fir.Id, "LIRR_TS_CTR", PositionType.Ctr, PositionKind.Acc, "Roma Radar TS", "132.700", 11);
        var papp = P(fir.Id, "LIRP_APP", PositionType.App, PositionKind.Airport, "Pisa Avvicinamento", "124.500", 20);
        papp.ApproachKind = ApproachKind.Standalone;
        var ptwr = P(fir.Id, "LIRP_TWR", PositionType.Twr, PositionKind.Airport, "Pisa Torre", "118.300", 30);
        var ftwr = P(fir.Id, "LIRF_TWR", PositionType.Twr, PositionKind.Airport, "Fiumicino Torre", "118.700", 30);
        db.Positions.AddRange(ne, ew, su, es, ts, papp, ptwr, ftwr);
        await db.SaveChangesAsync(ct);

        var byKey = sectors.ToDictionary(s => s.Key.Replace("LIRR-", ""), s => s.Id);

        // --- Ownership di default (PositionSector) ---
        db.PositionSectors.AddRange(
            PS(ne.Id, byKey["NE"]),
            PS(ew.Id, byKey["EW"]),
            PS(su.Id, byKey["SU"]), PS(su.Id, byKey["ES"]),  // SU possiede SU+ES "da solo"
            PS(es.Id, byKey["ES"]),
            PS(ts.Id, byKey["TS"]),
            PS(papp.Id, byKey["PISA"]),
            PS(ptwr.Id, byKey["PISA_TWR"]));

        // --- Gerarchia top-down (HierarchyRelation) ---
        db.HierarchyRelations.AddRange(
            HR(fir.Id, ne.Id, ts.Id),     // TS sotto NE
            HR(fir.Id, su.Id, es.Id),     // ES sotto SU
            HR(fir.Id, ne.Id, papp.Id),   // Pisa APP sotto NE
            HR(fir.Id, papp.Id, ptwr.Id), // Pisa TWR sotto Pisa APP
            HR(fir.Id, ne.Id, ftwr.Id));  // Fiumicino TWR sotto NE

        // --- Frequenze (una primaria per posizione) ---
        db.Frequencies.AddRange(
            F(ne.Id, "Roma Radar NE", "LIRR_NE_CTR", "128.800", true),
            F(su.Id, "Roma Radar SU", "LIRR_SU_CTR", "125.100", true),
            F(ew.Id, "Roma Radar EW", "LIRR_EW_CTR", "133.250", true),
            F(ts.Id, "Roma Radar TS", "LIRR_TS_CTR", "132.700", true),
            F(ptwr.Id, "Pisa Torre", "LIRP_TWR", "118.300", true),
            F(ftwr.Id, "Fiumicino Torre", "LIRF_TWR", "118.700", true));

        // --- Regole di unificazione ---
        db.UnificationRules.Add(new UnificationRule
        {
            FirId = fir.Id,
            Name = "Split SU/ES",
            Priority = 10,
            ConditionJson = """{"online":["LIRR_ES_CTR"]}""",
            AssignmentJson = """{"LIRR-ES":"LIRR_ES_CTR"}""",
            IsActive = true,
        });

        await db.SaveChangesAsync(ct);
        return fir.Id;
    }

    private static Sector S(int firId, string key, string name) =>
        new() { FirId = firId, Key = $"LIRR-{key}", Name = name };

    private static Position P(int firId, string callsign, PositionType type, PositionKind kind,
        string name, string freq, int coverage) =>
        new()
        {
            FirId = firId, Callsign = callsign, Type = type, Kind = kind,
            Name = name, DefaultFrequency = freq, CoverageOrder = coverage,
            ImportedAtUtc = DateTime.UtcNow, IsActive = true,
        };

    private static PositionSector PS(int positionId, int sectorId) =>
        new() { PositionId = positionId, SectorId = sectorId };

    private static HierarchyRelation HR(int firId, int parentId, int childId) =>
        new() { FirId = firId, ParentPositionId = parentId, ChildPositionId = childId };

    private static Frequency F(int positionId, string label, string callsign, string mhz, bool primary) =>
        new() { PositionId = positionId, Label = label, Callsign = callsign, FrequencyMhz = mhz, IsPrimary = primary };
}
