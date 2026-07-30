using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IVloaDerivationRepository"/>
public sealed class EfVloaDerivationRepository : IVloaDerivationRepository
{
    private readonly VipiDbContext _db;
    public EfVloaDerivationRepository(VipiDbContext db) => _db = db;

    public async Task<VloaPairInfo?> GetPairAsync(int docId, CancellationToken ct = default)
    {
        var doc = await _db.Documents
            .Include(d => d.Parties).ThenInclude(p => p.Sector).ThenInclude(s => s!.Acc)
            .FirstOrDefaultAsync(d => d.Id == docId && d.Type == DocumentType.Vloa, ct);
        if (doc is null) return null;

        var homeAcc = doc.Parties.FirstOrDefault(p => p.Role == PartyRole.Home)?.Sector?.Acc;
        var foreignAcc = doc.Parties.FirstOrDefault(p => p.Role == PartyRole.Neighbour)?.Sector?.Acc;
        if (homeAcc is null || foreignAcc is null) return null;

        var homeAll = await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == homeAcc.Code && s.IsActive).Select(s => s.Callsign).ToListAsync(ct);
        var foreignAll = await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == foreignAcc.Code && s.IsActive).Select(s => s.Callsign).ToListAsync(ct);

        var cand = await _db.NeighbourCandidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VloaDocumentId == docId, ct);
        var homeConfining = Deserialize(cand?.AdjacentHomeCallsigns);
        var foreignConfining = Deserialize(cand?.AdjacentForeignCallsigns);
        if (homeConfining.Count == 0) homeConfining = await BoundarySectorsAsync(homeAcc.Code, ct);
        if (foreignConfining.Count == 0) foreignConfining = await BoundarySectorsAsync(foreignAcc.Code, ct);

        // Codice nazione estero: IVAO CountryId del candidato se disponibile, altrimenti prefisso ICAO dell'ACC.
        var foreignCountry = string.IsNullOrWhiteSpace(cand?.CountryId) ? foreignAcc.CountryPrefix : cand!.CountryId;

        return new VloaPairInfo(homeAcc.Code, foreignAcc.Code, homeAcc.Name, foreignAcc.Name,
            homeConfining, foreignConfining, homeAll, foreignAll, foreignCountry);
    }

    public async Task<IReadOnlyList<VloaSectorPoly>> GetBoundaryPolygonsAsync(string accCode, CancellationToken ct = default) =>
        await _db.AccSectors.AsNoTracking()
            .Where(s => s.CenterId == accCode && !s.IsHidden && s.RegionMapPolygon != null && s.RegionMapPolygon != ""
                        && s.Position != null && (s.Position.ToUpper() == "CTR" || s.Position.ToUpper() == "FSS"))
            .Select(s => new VloaSectorPoly(s.ComposePosition, s.RegionMapPolygon!))
            .ToListAsync(ct);

    // vLOA usa la side-entity unificata DocumentProfile (doc 08i): stessi campi Hidden AoR/Freq/Sezioni; i campi extra
    // (FreqLinks/CoordTemplate) restano null per le vLOA. La tabella VloaProfiles è stata eliminata.
    public async Task<VloaEditorialState> LoadEditorialAsync(int docId, CancellationToken ct = default)
    {
        var p = await _db.DocumentProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.DocumentId == docId, ct);
        return new VloaEditorialState(Deserialize(p?.HiddenAorSectorsJson), Deserialize(p?.HiddenFrequenciesJson));
    }

    public async Task SaveEditorialAsync(int docId, VloaEditorialState state, CancellationToken ct = default)
    {
        var p = await _db.DocumentProfiles.FirstOrDefaultAsync(x => x.DocumentId == docId, ct);
        if (p is null)
        {
            p = new DocumentProfile { DocumentId = docId };
            _db.DocumentProfiles.Add(p);
        }
        p.HiddenAorSectorsJson = JsonSerializer.Serialize(state.HiddenAorSectors);
        p.HiddenFrequenciesJson = JsonSerializer.Serialize(state.HiddenFrequencies);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetHomeAccCodeAsync(int docId, CancellationToken ct = default) =>
        await _db.Documents.AsNoTracking()
            .Where(d => d.Id == docId && d.Type == DocumentType.Vloa)
            .SelectMany(d => d.Parties)
            .Where(p => p.Role == PartyRole.Home)
            .Select(p => p.Sector!.Acc!.Code)
            .FirstOrDefaultAsync(ct);

    /// <summary>Settori di confine (CTR/FSS) di un ACC dal catalogo, per il fallback dei settori confinanti.</summary>
    private async Task<List<string>> BoundarySectorsAsync(string accCode, CancellationToken ct) =>
        await _db.AccSectors.AsNoTracking()
            .Where(s => s.CenterId == accCode && !s.IsHidden && s.Position != null
                        && (s.Position.ToUpper() == "CTR" || s.Position.ToUpper() == "FSS"))
            .Select(s => s.ComposePosition).ToListAsync(ct);

    private static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }
}
