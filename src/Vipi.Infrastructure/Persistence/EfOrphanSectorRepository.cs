using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Impl. EF di <see cref="IOrphanSectorRepository"/>.
///
/// <para><b>Che cos'è un orfano.</b> Un <see cref="Sector"/> <c>IsProjected</c> e <c>!IsActive</c>: la
/// proiezione l'ha disattivato perché il suo callsign non è più nell'insieme visibile dei cataloghi. Due
/// cause distinte, che la riga distingue: il callsign non c'è proprio più (la sorgente l'ha tolto) oppure
/// c'è ma è nascosto (l'ha deciso un admin).</para>
///
/// <para>⚠️ <b>I bloccanti si calcolano prima, non si scoprono dopo.</b> Cinque relazioni verso
/// <c>Sector</c> sono <c>Restrict</c> (sotto-settori, parti di documento, blocchi con scope/da/a, i due lati
/// di un accordo): senza questo controllo la cancellazione arriverebbe fino al database e tornerebbe indietro
/// come <c>DbUpdateException</c> — un messaggio che parla di vincoli e nomi di colonne a un utente che voleva
/// solo togliere una riga. Qui diventano frasi che dicono <b>chi</b> lo trattiene.</para>
/// </summary>
public sealed class EfOrphanSectorRepository : IOrphanSectorRepository
{
    private readonly VipiDbContext _db;
    private readonly IDocumentImpactRepository _impacts;

    public EfOrphanSectorRepository(VipiDbContext db, IDocumentImpactRepository impacts)
    {
        _db = db;
        _impacts = impacts;
    }

    public async Task<IReadOnlyList<OrphanSectorRow>> ListOrphansAsync(string? accCode, CancellationToken ct = default)
    {
        var q = _db.Sectors.AsNoTracking().Where(s => s.IsProjected && !s.IsActive);
        if (!string.IsNullOrWhiteSpace(accCode)) q = q.Where(s => s.Acc!.Code == accCode);

        var orfani = await q
            .OrderBy(s => s.Acc!.Code).ThenBy(s => s.Callsign)
            .Select(s => new { s.Id, s.Callsign, s.Name, AccCode = s.Acc!.Code, s.DocumentId, Titolo = s.Document!.Title })
            .ToListAsync(ct);
        if (orfani.Count == 0) return Array.Empty<OrphanSectorRow>();

        var righe = new List<OrphanSectorRow>(orfani.Count);
        foreach (var o in orfani)
        {
            var inCatalogo = await InCatalogoAsync(o.Callsign, ct);
            righe.Add(new OrphanSectorRow(
                o.Id, o.Callsign, o.Name, o.AccCode, inCatalogo, o.DocumentId, o.Titolo,
                await _impacts.FindDocumentsForSectorAsync(o.Callsign, o.AccCode, ct),
                await BloccantiAsync(o.Id, o.Callsign, ct)));
        }
        return righe;
    }

    public async Task<OrphanSectorRow?> GetOrphanAsync(int sectorId, CancellationToken ct = default)
    {
        var s = await _db.Sectors.AsNoTracking()
            .Where(x => x.Id == sectorId && x.IsProjected && !x.IsActive)
            .Select(x => new { x.Id, x.Callsign, x.Name, AccCode = x.Acc!.Code, x.DocumentId, Titolo = x.Document!.Title })
            .FirstOrDefaultAsync(ct);
        if (s is null) return null;

        return new OrphanSectorRow(
            s.Id, s.Callsign, s.Name, s.AccCode, await InCatalogoAsync(s.Callsign, ct), s.DocumentId, s.Titolo,
            await _impacts.FindDocumentsForSectorAsync(s.Callsign, s.AccCode, ct),
            await BloccantiAsync(s.Id, s.Callsign, ct));
    }

    private async Task<bool> InCatalogoAsync(string callsign, CancellationToken ct) =>
        await _db.AccSectors.AsNoTracking().AnyAsync(x => x.ComposePosition == callsign, ct)
        || await _db.AirportSectors.AsNoTracking().AnyAsync(x => x.ComposePosition == callsign, ct);

    /// <summary>Chi trattiene il settore, in frasi. L'ordine è quello in cui conviene risolverli.</summary>
    private async Task<IReadOnlyList<string>> BloccantiAsync(int sectorId, string callsign, CancellationToken ct)
    {
        var motivi = new List<string>();

        var figli = await _db.Sectors.AsNoTracking().CountAsync(s => s.ParentSectorId == sectorId, ct);
        if (figli > 0) motivi.Add($"ha {figli} sotto-settori che vi puntano");

        var accordi = await _db.CoordinationAgreements.AsNoTracking()
            .CountAsync(a => a.SideASectorId == sectorId || a.SideBSectorId == sectorId, ct);
        if (accordi > 0) motivi.Add($"e' un lato di {accordi} accordi di coordinamento");

        var parti = await _db.DocumentParties.AsNoTracking().CountAsync(p => p.SectorId == sectorId, ct);
        if (parti > 0) motivi.Add($"e' una parte di {parti} vLOA");

        var blocchi = await _db.ContentBlocks.AsNoTracking().CountAsync(
            b => b.ScopeSectorId == sectorId || b.FromSectorId == sectorId || b.ToSectorId == sectorId, ct);
        if (blocchi > 0) motivi.Add($"e' citato da {blocchi} blocchi di contenuto");

        return motivi;
    }

    public async Task<IReadOnlyList<ReattachTargetRow>> ReattachTargetsAsync(int orphanSectorId, CancellationToken ct = default)
    {
        var accId = await _db.Sectors.AsNoTracking()
            .Where(s => s.Id == orphanSectorId).Select(s => (int?)s.AccId).FirstOrDefaultAsync(ct);
        if (accId is not int aid) return Array.Empty<ReattachTargetRow>();

        return await _db.Sectors.AsNoTracking()
            .Where(s => s.AccId == aid && s.IsActive && s.Id != orphanSectorId)
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => new ReattachTargetRow(s.Id, s.Callsign, s.Name))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Sposta il documento dall'orfano al bersaglio. ⚠️ Se il bersaglio ne ha già uno <b>diverso</b> ci si
    /// ferma: sovrascriverlo vorrebbe dire sganciare in silenzio un altro documento, che è esattamente il
    /// guasto che questa pagina esiste per riparare.
    /// </summary>
    public async Task ReattachAsync(int orphanSectorId, int targetSectorId, CancellationToken ct = default)
    {
        var orfano = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == orphanSectorId, ct)
                     ?? throw new Vipi.Application.Aor.ValidationException("Settore orfano inesistente.");
        var bersaglio = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == targetSectorId, ct)
                        ?? throw new Vipi.Application.Aor.ValidationException("Settore di destinazione inesistente.");
        if (orfano.DocumentId is not int docId)
            throw new Vipi.Application.Aor.ValidationException("Questo orfano non porta nessun documento.");
        if (bersaglio.DocumentId is int altro && altro != docId)
            throw new Vipi.Application.Aor.ValidationException(
                $"{bersaglio.Callsign} descrive già un altro documento: scegli un settore libero.");

        bersaglio.DocumentId = docId;
        bersaglio.IsPrimary = orfano.IsPrimary || bersaglio.IsPrimary;
        bersaglio.FeaturedRank ??= orfano.FeaturedRank;

        orfano.DocumentId = null;
        orfano.IsPrimary = false;
        orfano.FeaturedRank = null;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(int orphanSectorId, CancellationToken ct = default)
    {
        var s = await _db.Sectors.FirstOrDefaultAsync(x => x.Id == orphanSectorId, ct);
        if (s is null) return;

        // La riga di catalogo, se la sorgente la espone ancora (caso «nascosto»): togliere solo la proiezione
        // la farebbe tornare al primo sync, e l'utente vedrebbe il settore risorgere senza spiegazione.
        var acc = await _db.AccSectors.Where(x => x.ComposePosition == s.Callsign).ToListAsync(ct);
        if (acc.Count > 0) _db.AccSectors.RemoveRange(acc);
        var apt = await _db.AirportSectors.Where(x => x.ComposePosition == s.Callsign).ToListAsync(ct);
        if (apt.Count > 0) _db.AirportSectors.RemoveRange(apt);

        _db.Sectors.Remove(s);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetAccCodeAsync(int sectorId, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking().Where(s => s.Id == sectorId && s.Acc != null)
            .Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);
}
