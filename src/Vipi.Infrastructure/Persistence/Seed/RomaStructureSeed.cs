using Microsoft.EntityFrameworkCore;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence.Seed;

/// <summary>
/// Seed strutturale (no contenuti) della ACC pilota Roma (LIRR): settori (entità unificata) + contenimento
/// top-down (ParentSectorId) + frequenze. PIANO §17.1. Idempotente: no-op se LIRR esiste già.
/// In produzione i settori arriveranno dalle API IVAO; il contenimento resta dato manuale.
/// Lo split SU/ES è ora pura gerarchia (ES figlio di SU): nessuna UnificationRule necessaria nel seed.
/// </summary>
public static class RomaStructureSeed
{
    public const string AccCode = "LIRR";

    public static async Task<int> SeedAsync(VipiDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Accs.FirstOrDefaultAsync(f => f.Code == AccCode, ct);
        if (existing is not null) return existing.Id;

        var acc = new Acc { Code = AccCode, Name = "Roma ACC", CountryPrefix = "LI" };
        db.Accs.Add(acc);
        await db.SaveChangesAsync(ct); // serve l'Id per le FK seguenti

        // --- Aeroporti della ACC (entità di prima classe) ---
        var lirp = new Airport { AccId = acc.Id, Icao = "LIRP", Name = "Pisa" };
        var lirf = new Airport { AccId = acc.Id, Icao = "LIRF", Name = "Roma Fiumicino" };
        db.Airports.AddRange(lirp, lirf);
        await db.SaveChangesAsync(ct);

        // --- Settori radice (ACC d'area) ---
        var ne = S(acc.Id, "LIRR_NE_CTR", SectorType.Ctr, SectorKind.Acc, "Roma Radar NE", "128.800", 10);
        var ew = S(acc.Id, "LIRR_EW_CTR", SectorType.Ctr, SectorKind.Acc, "Roma Radar EW", "133.250", 10);
        var su = S(acc.Id, "LIRR_SU_CTR", SectorType.Ctr, SectorKind.Acc, "Roma Radar SU", "125.100", 10);
        db.Sectors.AddRange(ne, ew, su);
        await db.SaveChangesAsync(ct);

        // --- Sotto-settori e aeroporti (contenimento top-down via ParentSectorId) ---
        var es = S(acc.Id, "LIRR_ES_CTR", SectorType.Ctr, SectorKind.Acc, "Roma Radar ES", "129.350", 11, parent: su.Id);
        var ts = S(acc.Id, "LIRR_TS_CTR", SectorType.Ctr, SectorKind.Acc, "Roma Radar TS", "132.700", 11, parent: ne.Id);
        var papp = S(acc.Id, "LIRP_APP", SectorType.App, SectorKind.Airport, "Pisa Avvicinamento", "124.500", 20, parent: ne.Id, airport: lirp);
        papp.ApproachKind = ApproachKind.Standalone;
        db.Sectors.AddRange(es, ts, papp);
        await db.SaveChangesAsync(ct);

        var ptwr = S(acc.Id, "LIRP_TWR", SectorType.Twr, SectorKind.Airport, "Pisa Torre", "118.300", 30, parent: papp.Id, airport: lirp);
        var ftwr = S(acc.Id, "LIRF_TWR", SectorType.Twr, SectorKind.Airport, "Fiumicino Torre", "118.700", 30, parent: ne.Id, airport: lirf);
        db.Sectors.AddRange(ptwr, ftwr);
        await db.SaveChangesAsync(ct);

        // --- Frequenze (una primaria per settore) ---
        db.Frequencies.AddRange(
            F(ne.Id, "Roma Radar NE", "LIRR_NE_CTR", "128.800", true),
            F(su.Id, "Roma Radar SU", "LIRR_SU_CTR", "125.100", true),
            F(ew.Id, "Roma Radar EW", "LIRR_EW_CTR", "133.250", true),
            F(ts.Id, "Roma Radar TS", "LIRR_TS_CTR", "132.700", true),
            F(ptwr.Id, "Pisa Torre", "LIRP_TWR", "118.300", true),
            F(ftwr.Id, "Fiumicino Torre", "LIRF_TWR", "118.700", true));

        await db.SaveChangesAsync(ct);
        return acc.Id;
    }

    private static Sector S(int accId, string callsign, SectorType type, SectorKind kind,
        string name, string freq, int coverage, int? parent = null, Airport? airport = null) =>
        new()
        {
            AccId = accId, Callsign = callsign, Type = type, Kind = kind, Name = name,
            DefaultFrequency = freq, CoverageOrder = coverage, ParentSectorId = parent,
            Airport = airport, AirportIcao = airport?.Icao,
            ImportedAtUtc = DateTime.UtcNow, IsActive = true,
        };

    private static Frequency F(int sectorId, string label, string callsign, string mhz, bool primary) =>
        new() { SectorId = sectorId, Label = label, Callsign = callsign, FrequencyMhz = mhz, IsPrimary = primary };
}
