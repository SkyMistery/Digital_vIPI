using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF Core di <see cref="IContentRepository"/>.</summary>
public sealed class EfContentRepository : IContentRepository
{
    private readonly VipiDbContext _db;

    public EfContentRepository(VipiDbContext db) => _db = db;

    public Task<RawDocument?> LoadAccVipiAsync(string accCode, CancellationToken ct = default)
    {
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vipi
                 && d.Status == DocumentStatus.Published
                 && d.Sectors.Any(s => s.Kind == SectorKind.Acc && s.Acc!.Code == accCode),
            ct);
    }

    public Task<RawDocument?> LoadAirportVipiAsync(string icao, CancellationToken ct = default)
    {
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vipi
                 && d.Status == DocumentStatus.Published
                 && d.Sectors.Any(s => s.Kind == SectorKind.Airport && s.AirportIcao == icao)
                 // Aeroporto nascosto dall'admin ⇒ pagina pubblica inaccessibile.
                 && !_db.Airports.Any(a => a.Icao == icao && a.IsHidden),
            ct);
    }

    public Task<RawDocument?> LoadVloaAsync(string accCode, CancellationToken ct = default)
    {
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vloa
                 && d.Status == DocumentStatus.Published
                 && d.Parties.Any(pa => pa.Role == PartyRole.Home && pa.Sector!.Acc!.Code == accCode),
            ct);
    }

    public Task<RawDocument?> LoadVloaByIdAsync(int docId, CancellationToken ct = default)
    {
        return LoadVipiAsync(
            d => d.Type == DocumentType.Vloa
                 && d.Status == DocumentStatus.Published
                 && d.Id == docId,
            ct);
    }

    private async Task<RawDocument?> LoadVipiAsync(
        System.Linq.Expressions.Expression<Func<Document, bool>> predicate, CancellationToken ct)
    {
        var doc = await _db.Documents
            .AsNoTracking()
            .Where(predicate)
            .FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        var versionId = doc.CurrentVersionId
            ?? await _db.DocumentVersions
                .Where(v => v.DocumentId == doc.Id && v.Status == DocumentStatus.Published)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => (int?)v.Id)
                .FirstOrDefaultAsync(ct);
        if (versionId is null) return null;

        var sections = await _db.DocumentSections
            .Where(s => s.DocumentVersionId == versionId)
            .AsNoTracking().ToListAsync(ct);

        var blocks = await _db.ContentBlocks
            .Where(b => b.DocumentVersionId == versionId)
            .Include(b => b.ScopeSector)
            .AsNoTracking().ToListAsync(ct);

        var blocksBySection = blocks
            .GroupBy(b => b.SectionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(b => b.Order).ToList());

        var childrenByParent = sections
            .Where(s => s.ParentSectionId != null)
            .GroupBy(s => s.ParentSectionId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Order).ToList());

        RawSection Build(DocumentSection s) => new()
        {
            Id = s.Id,
            Title = s.Title,
            Depth = s.Depth,
            Kind = s.SectionKind,
            Order = s.Order,
            Blocks = (blocksBySection.TryGetValue(s.Id, out var bs) ? bs : new())
                .Select(MapBlock).ToList(),
            Children = (childrenByParent.TryGetValue(s.Id, out var cs) ? cs : new())
                .Select(Build).ToList(),
        };

        var roots = sections
            .Where(s => s.ParentSectionId is null)
            .OrderBy(s => s.Order)
            .Select(Build).ToList();

        return new RawDocument
        {
            Title = doc.Title,
            AiracCycle = doc.LastUpdatedAiracCycle,
            Roots = roots,
        };
    }

    private static RawBlock MapBlock(ContentBlock b) => new()
    {
        Id = b.Id,
        Order = b.Order,
        Format = b.Format,
        Visibility = b.Visibility,
        Tier = b.Tier,
        ScopeSectorKey = b.ScopeSector?.Callsign,
        Body = b.Body,
        BodyJson = b.BodyJson,
        CalloutKind = b.CalloutKind,
    };
}
