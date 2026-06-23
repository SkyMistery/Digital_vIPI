using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IEditGrantRepository"/>.</summary>
public sealed class EfEditGrantRepository : IEditGrantRepository
{
    private readonly VipiDbContext _db;
    public EfEditGrantRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<GrantRow>> ListAsync(CancellationToken ct = default)
    {
        return await _db.EditGrants
            .Include(g => g.Fir)
            .OrderBy(g => g.Fir!.Code).ThenBy(g => g.Vid)
            .Select(g => new GrantRow
            {
                Id = g.Id, Vid = g.Vid, DisplayName = g.DisplayName,
                FirCode = g.Fir!.Code, GrantedByVid = g.GrantedByVid, GrantedAtUtc = g.GrantedAtUtc,
            })
            .ToListAsync(ct);
    }

    public async Task<int> AddAsync(int vid, string? displayName, string firCode, int grantedByVid, CancellationToken ct = default)
    {
        var firId = await _db.Firs.Where(f => f.Code == firCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"FIR {firCode} inesistente.");

        var existing = await _db.EditGrants.FirstOrDefaultAsync(g => g.Vid == vid && g.FirId == firId, ct);
        if (existing is not null)
        {
            existing.DisplayName = displayName;     // aggiorna nome, evita duplicati (indice unico)
            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }

        var grant = new EditGrant
        {
            Vid = vid, DisplayName = displayName, FirId = firId,
            GrantedByVid = grantedByVid, GrantedAtUtc = DateTime.UtcNow,
        };
        _db.EditGrants.Add(grant);
        await _db.SaveChangesAsync(ct);
        return grant.Id;
    }

    public async Task RevokeAsync(int grantId, CancellationToken ct = default)
    {
        var g = await _db.EditGrants.FirstOrDefaultAsync(x => x.Id == grantId, ct);
        if (g is null) return;
        _db.EditGrants.Remove(g);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> HasGrantAsync(int vid, string firCode, CancellationToken ct = default) =>
        _db.EditGrants.AnyAsync(g => g.Vid == vid && g.Fir!.Code == firCode, ct);

    public async Task<string?> GetDocumentFirCodeAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await _db.Documents
            .Include(d => d.ScopePosition).ThenInclude(p => p!.Fir)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return null;

        if (doc.ScopePosition?.Fir is not null) return doc.ScopePosition.Fir.Code;

        // vLOA: niente ScopePosition → FIR della parte Home.
        return await _db.DocumentParties
            .Where(pa => pa.DocumentId == documentId && pa.Role == PartyRole.Home)
            .Select(pa => pa.Position!.Fir!.Code)
            .FirstOrDefaultAsync(ct);
    }
}
