using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IChangesRepository"/>.</summary>
public sealed class EfChangesRepository : IChangesRepository
{
    private readonly VipiDbContext _db;
    public EfChangesRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<ChangeRow>> ListChangedAsync(string airacCycle, CancellationToken ct = default)
    {
        var docs = await _db.Documents
            .Where(d => d.CurrentVersionId != null)
            .Include(d => d.Sectors).ThenInclude(s => s.Fir)
            .Include(d => d.CurrentVersion)
            .AsNoTracking().ToListAsync(ct);

        var rows = new List<ChangeRow>();
        foreach (var d in docs)
        {
            var cur = d.CurrentVersion!;
            if (cur.AiracCycle != airacCycle) continue;

            // FIR + rotta
            string? fir; string urlBase;
            if (d.Type == DocumentType.Vloa)
            {
                fir = await _db.DocumentParties.Where(pa => pa.DocumentId == d.Id && pa.Role == PartyRole.Home)
                    .Select(pa => pa.Sector!.Fir!.Code).FirstOrDefaultAsync(ct);
                urlBase = $"vloa/{d.Id}";
            }
            else
            {
                var primary = d.Sectors.FirstOrDefault(x => x.IsPrimary) ?? d.Sectors.FirstOrDefault();
                fir = primary?.Fir?.Code;
                urlBase = primary?.Kind == SectorKind.Airport
                    ? (primary?.AirportIcao is string ic ? $"airports?icao={ic}" : "airports")
                    : "vipi";
            }
            if (fir is null) continue;

            // versione precedente (numero più alto < corrente)
            var prevVersionId = await _db.DocumentVersions
                .Where(v => v.DocumentId == d.Id && v.VersionNumber < cur.VersionNumber)
                .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);

            var currBlocks = await _db.ContentBlocks.CountAsync(b => b.DocumentVersionId == cur.Id, ct);
            var currSections = await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == cur.Id, ct);
            var prevBlocks = prevVersionId is int pv ? await _db.ContentBlocks.CountAsync(b => b.DocumentVersionId == pv, ct) : 0;
            var prevSections = prevVersionId is int pv2 ? await _db.DocumentSections.CountAsync(s => s.DocumentVersionId == pv2, ct) : 0;

            rows.Add(new ChangeRow
            {
                DocTitle = d.Title,
                Type = d.Type,
                FirCode = fir,
                Url = $"/vsop/{fir.ToLowerInvariant()}/{urlBase}",
                VersionNumber = cur.VersionNumber,
                Note = cur.Note,
                PublishedByUserId = cur.CreatedByUserId,
                PublishedUtc = cur.CreatedUtc,
                PrevBlocks = prevBlocks,
                CurrBlocks = currBlocks,
                PrevSections = prevSections,
                CurrSections = currSections,
            });
        }

        return rows.OrderByDescending(r => r.PublishedUtc).ToList();
    }
}
