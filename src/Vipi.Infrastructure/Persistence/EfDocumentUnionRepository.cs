using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IDocumentUnionRepository"/>
public sealed class EfDocumentUnionRepository : IDocumentUnionRepository
{
    private readonly VipiDbContext _db;
    public EfDocumentUnionRepository(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<UnionRow>> ListAsync(CancellationToken ct = default) =>
        await _db.DocumentUnionMembers.AsNoTracking()
            .OrderBy(m => m.UnionId).ThenBy(m => m.Order)
            .Select(m => new UnionRow(m.UnionId, m.Id, m.DocumentId, m.Order))
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<UnionRow>> ByDocumentAsync(int documentId, CancellationToken ct = default)
    {
        // Due passi e non una sotto-query: il primo dice SE il documento è unito, e nel caso normale — che è
        // «non lo è» — il secondo non parte affatto. Questa domanda la fa ogni apertura di ogni viewer.
        var unionId = await _db.DocumentUnionMembers.AsNoTracking()
            .Where(m => m.DocumentId == documentId)
            .Select(m => (int?)m.UnionId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return unionId is null ? Array.Empty<UnionRow>() : await ByUnionAsync(unionId.Value, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UnionRow>> ByUnionAsync(int unionId, CancellationToken ct = default) =>
        await _db.DocumentUnionMembers.AsNoTracking()
            .Where(m => m.UnionId == unionId)
            .OrderBy(m => m.Order)
            .Select(m => new UnionRow(m.UnionId, m.Id, m.DocumentId, m.Order))
            .ToListAsync(ct).ConfigureAwait(false);

    public async Task<int> CreateAsync(int hostDocumentId, int guestDocumentId, int createdByUserId,
                                       CancellationToken ct = default)
    {
        var unione = new DocumentUnion { CreatedUtc = DateTime.UtcNow, CreatedByUserId = createdByUserId };
        unione.Members.Add(new DocumentUnionMember { DocumentId = hostDocumentId, Order = 0 });
        unione.Members.Add(new DocumentUnionMember { DocumentId = guestDocumentId, Order = 1 });
        _db.DocumentUnions.Add(unione);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return unione.Id;
    }

    public async Task AddMemberAsync(int unionId, int documentId, CancellationToken ct = default)
    {
        var coda = await _db.DocumentUnionMembers
            .Where(m => m.UnionId == unionId)
            .MaxAsync(m => (int?)m.Order, ct).ConfigureAwait(false) ?? -1;
        _db.DocumentUnionMembers.Add(new DocumentUnionMember
        {
            UnionId = unionId,
            DocumentId = documentId,
            Order = coda + 1,
        });
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task RemoveMemberAsync(int memberId, CancellationToken ct = default)
    {
        // ⚠️ Niente ExecuteDelete: desincronizza il change-tracker, e qui subito dopo si rinumerano i
        // fratelli con lo stesso context (memoria: EF ExecuteDelete nei repository).
        var riga = await _db.DocumentUnionMembers.FirstOrDefaultAsync(m => m.Id == memberId, ct)
                                                 .ConfigureAwait(false);
        if (riga is null) return;

        var unionId = riga.UnionId;
        _db.DocumentUnionMembers.Remove(riga);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await RinumeraAsync(unionId, ct).ConfigureAwait(false);
    }

    public async Task DissolveAsync(int unionId, CancellationToken ct = default)
    {
        var unione = await _db.DocumentUnions
            .Include(u => u.Members)
            .FirstOrDefaultAsync(u => u.Id == unionId, ct).ConfigureAwait(false);
        if (unione is null) return;
        // I membri se ne vanno con la cascata; i DOCUMENTI non si toccano — sciogliere non è eliminare.
        _db.DocumentUnions.Remove(unione);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task MoveAsync(int memberId, int delta, CancellationToken ct = default)
    {
        if (delta == 0) return;

        var riga = await _db.DocumentUnionMembers.FirstOrDefaultAsync(m => m.Id == memberId, ct)
                                                 .ConfigureAwait(false);
        if (riga is null) return;

        var fratelli = await _db.DocumentUnionMembers
            .Where(m => m.UnionId == riga.UnionId)
            .OrderBy(m => m.Order)
            .ToListAsync(ct).ConfigureAwait(false);

        var i = fratelli.FindIndex(m => m.Id == memberId);
        var j = i + Math.Sign(delta);
        // Ai bordi non si fa niente e non si protesta: è il tasto che deve essere spento, non l'operazione
        // che deve esplodere — la stessa scelta di MoveSectionAsync.
        if (i < 0 || j < 0 || j >= fratelli.Count) return;

        (fratelli[i].Order, fratelli[j].Order) = (fratelli[j].Order, fratelli[i].Order);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> TidyAsync(CancellationToken ct = default)
    {
        // Le unioni rimaste con meno di due membri. La FK cascade ha già portato via le righe dei documenti
        // eliminati: quel che resta da chiudere è l'unione che quelle righe tenevano in piedi.
        var magre = await _db.DocumentUnions
            .Include(u => u.Members)
            .Where(u => u.Members.Count < 2)
            .ToListAsync(ct).ConfigureAwait(false);
        if (magre.Count == 0) return 0;

        _db.DocumentUnions.RemoveRange(magre);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return magre.Count;
    }

    /// <summary>Riporta le posizioni a 0,1,2… dopo una rimozione: un buco nell'ordine non rompe niente, ma
    /// rende «sposta giù» un gesto che a volte non muove nulla.</summary>
    private async Task RinumeraAsync(int unionId, CancellationToken ct)
    {
        var fratelli = await _db.DocumentUnionMembers
            .Where(m => m.UnionId == unionId)
            .OrderBy(m => m.Order)
            .ToListAsync(ct).ConfigureAwait(false);

        var cambiato = false;
        for (var i = 0; i < fratelli.Count; i++)
            if (fratelli[i].Order != i) { fratelli[i].Order = i; cambiato = true; }

        if (cambiato) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
