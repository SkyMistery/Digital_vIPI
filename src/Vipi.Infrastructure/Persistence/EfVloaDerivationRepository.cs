using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IVloaDerivationRepository"/>
internal sealed class EfVloaDerivationRepository : IVloaDerivationRepository
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

        // U-158: i confinanti della coppia li calcola la derivazione dalla geometria (VloaConfinanti); qui c'erano
        // un secondo elenco e il suo ripiego sul catalogo intero, che nessuno leggeva.
        var cand = await _db.NeighbourCandidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VloaDocumentId == docId, ct);

        // Codice nazione estero: IVAO CountryId del candidato se disponibile, altrimenti prefisso ICAO dell'ACC.
        var foreignCountry = string.IsNullOrWhiteSpace(cand?.CountryId) ? foreignAcc.CountryPrefix : cand!.CountryId;

        var homeInattivi = await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == homeAcc.Code && !s.IsActive).Select(s => s.Callsign).ToListAsync(ct);
        var foreignInattivi = await _db.Sectors.AsNoTracking()
            .Where(s => s.Acc!.Code == foreignAcc.Code && !s.IsActive).Select(s => s.Callsign).ToListAsync(ct);

        return new VloaPairInfo(homeAcc.Code, foreignAcc.Code, homeAcc.Name, foreignAcc.Name,
            homeAll, foreignAll, foreignCountry, homeInattivi, foreignInattivi);
    }

    public async Task<IReadOnlyList<VloaSectorPoly>> GetBoundaryPolygonsAsync(string accCode, CancellationToken ct = default) =>
        await PoligoniDiConfine(_db, accCode).ToListAsync(ct);

    /// <summary>I poligoni di confine di un ACC (CTR/FSS visibili, con la forma): la sola sorgente della geometria
    /// dei confinanti, per la vLOA e per chi cerca i documenti da avvisare (U-158).</summary>
    internal static IQueryable<VloaSectorPoly> PoligoniDiConfine(VipiDbContext db, string accCode) =>
        db.AccSectors.AsNoTracking()
            .Where(s => s.CenterId == accCode && !s.IsHidden && s.RegionMapPolygon != null && s.RegionMapPolygon != ""
                        && s.Position != null && (s.Position.ToUpper() == "CTR" || s.Position.ToUpper() == "FSS"))
            .Select(s => new VloaSectorPoly(s.ComposePosition, s.RegionMapPolygon!));

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

    private static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return new List<string>(); }
    }
}
