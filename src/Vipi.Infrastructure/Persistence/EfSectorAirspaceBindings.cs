using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: gli agganci settore → volumi dell'AIP. Le regole stanno su <see cref="ISectorAirspaceBindings"/>.
///
/// <para>⚠️ <b>La risoluzione passa sempre dal caricamento IN VIGORE.</b> Un aggancio cita una chiave, non
/// una riga: cambiare il file in vigore cambia quale poligono quel settore disegna, ed è esattamente quel
/// che deve succedere quando arriva un AIRAC nuovo. Se la chiave nel file nuovo non c'è più, l'aggancio
/// resta <b>scoperto</b> e il settore torna alla forma di IVAO — non sparisce e non si cancella da sé.</para>
/// </summary>
public sealed class EfSectorAirspaceBindings : ISectorAirspaceBindings
{
    private readonly VipiDbContext _db;

    public EfSectorAirspaceBindings(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, SectorAirspaceBindingRow>> ResolveAsync(
        IReadOnlyList<string> callsigns, CancellationToken ct = default)
    {
        var chiesti = callsigns.Select(Norm).Where(c => c.Length > 0).Distinct().ToList();
        if (chiesti.Count == 0) return Vuoto;

        var agganci = await _db.SectorAirspaceBindings.AsNoTracking()
            .Where(b => chiesti.Contains(b.Callsign))
            .OrderBy(b => b.Callsign).ThenBy(b => b.Position).ToListAsync(ct);

        return await RisolviAsync(agganci, ct);
    }

    public async Task<IReadOnlyList<SectorAirspaceBindingRow>> ListAsync(CancellationToken ct = default)
    {
        var agganci = await _db.SectorAirspaceBindings.AsNoTracking()
            .OrderBy(b => b.Callsign).ThenBy(b => b.Position).ToListAsync(ct);

        return (await RisolviAsync(agganci, ct)).Values
            .OrderBy(r => r.Callsign, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task SetAsync(SourceCatalog catalog, int sectorId, string callsign,
        IReadOnlyList<AirspaceVolumeKey> volumes, int? userId, string? userName, CancellationToken ct = default)
    {
        var cs = Norm(callsign);

        // L'elenco SOSTITUISCE quello di prima: è una scelta, non un accumulo. Vuoto = torna a IVAO.
        var vecchi = await _db.SectorAirspaceBindings
            .Where(b => b.Catalog == catalog && b.SectorId == sectorId).ToListAsync(ct);
        _db.SectorAirspaceBindings.RemoveRange(vecchi);

        var quando = DateTime.UtcNow;
        var posizione = 0;
        foreach (var v in volumes.DistinctBy(v => (v.Key, v.Ordinal)))
        {
            _db.SectorAirspaceBindings.Add(new SectorAirspaceBinding
            {
                Catalog = catalog,
                SectorId = sectorId,
                Callsign = cs,
                VolumeKey = v.Key,
                VolumeOrdinal = v.Ordinal,
                Position = posizione++,
                CreatedUtc = quando,
                CreatedByUserId = userId,
                CreatedByName = userName,
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Da righe d'aggancio a righe risolte, con UNA query sui volumi del caricamento in vigore.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, SectorAirspaceBindingRow>> RisolviAsync(
        List<SectorAirspaceBinding> agganci, CancellationToken ct)
    {
        if (agganci.Count == 0) return Vuoto;

        var importId = await _db.AirspaceImports.AsNoTracking()
            .Where(i => i.IsCurrent).Select(i => (int?)i.Id).FirstOrDefaultAsync(ct);

        // Senza caricamento in vigore ogni aggancio è scoperto: il settore torna a IVAO, e la pagina lo dice.
        var chiavi = agganci.Select(b => b.VolumeKey).Distinct().ToList();
        var volumi = importId is null
            ? new List<AirspaceVolume>()
            : await _db.AirspaceVolumes.AsNoTracking()
                .Where(v => v.ImportId == importId && chiavi.Contains(v.NaturalKey)).ToListAsync(ct);

        var indice = volumi.ToDictionary(v => (v.NaturalKey, v.Ordinal));

        var esito = new Dictionary<string, SectorAirspaceBindingRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var gruppo in agganci.GroupBy(b => (b.Catalog, b.SectorId)))
        {
            var ordinati = gruppo.OrderBy(b => b.Position).ToList();
            var trovati = new List<AirspaceVolumeRow>();
            var mancanti = new List<AirspaceVolumeKey>();

            foreach (var b in ordinati)
            {
                if (indice.TryGetValue((b.VolumeKey, b.VolumeOrdinal), out var v)) trovati.Add(Riga(v));
                else mancanti.Add(new AirspaceVolumeKey(b.VolumeKey, b.VolumeOrdinal));
            }

            var primo = ordinati[0];
            esito[primo.Callsign] = new SectorAirspaceBindingRow(
                primo.Catalog, primo.SectorId, primo.Callsign, trovati, mancanti,
                primo.CreatedUtc, primo.CreatedByName);
        }
        return esito;
    }

    private static readonly IReadOnlyDictionary<string, SectorAirspaceBindingRow> Vuoto =
        new Dictionary<string, SectorAirspaceBindingRow>(StringComparer.OrdinalIgnoreCase);

    private static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();

    private static AirspaceVolumeRow Riga(AirspaceVolume v) => new(
        v.Id, v.ImportId, v.Family, v.Name, v.Category, v.AirspaceClass,
        v.BaseDatum, v.BaseFeet, v.BaseRaw, v.TopDatum, v.TopFeet, v.TopRaw,
        v.PolygonJson, v.RingCount, v.PointCount, v.NaturalKey, v.Ordinal,
        v.MinLat, v.MinLon, v.MaxLat, v.MaxLon);
}
