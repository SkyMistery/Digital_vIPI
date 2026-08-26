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

    public async Task<IReadOnlyList<OrphanSectorRow>> ListOrphansAsync(
        string? accCode, DateTime? sogliaTimbro, CancellationToken ct = default)
    {
        var q = _db.Sectors.AsNoTracking().Where(s => s.IsProjected && !s.IsActive);
        if (!string.IsNullOrWhiteSpace(accCode)) q = q.Where(s => s.Acc!.Code == accCode);

        var spenti = await q
            .OrderBy(s => s.Acc!.Code).ThenBy(s => s.Callsign)
            .Select(s => new { s.Id, s.Callsign, s.Name, AccCode = s.Acc!.Code, s.DocumentId, Titolo = s.Document!.Title })
            .ToListAsync(ct);

        var righe = new List<OrphanSectorRow>();
        foreach (var o in spenti)
            righe.Add(await RigaAsync(o.Id, o.Callsign, o.Name, o.AccCode, o.DocumentId, o.Titolo,
                await InCatalogoAsync(o.Callsign, ct) ? OrphanReason.Hidden : OrphanReason.Gone, null, ct));

        // Gli STANTÌI: attivi, in catalogo, ma la sorgente non li manda più. ⚠️ Dal 26 agosto 2026 questo NON
        // è più il posto dove si vedono le rinomine: quelle le riconosce l'identità della sorgente e sono già
        // applicate quando si arriva qui. Qui restano le sparizioni vere, dove la riga di catalogo sopravvive
        // solo perché i cataloghi non potano mai.
        if (sogliaTimbro is { } soglia)
        {
            var giaVisti = righe.Select(r => r.Callsign).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var st in await ListStaleCatalogRowsAsync(soglia, ct))
            {
                if (giaVisti.Contains(st.Callsign)) continue;
                if (!string.IsNullOrWhiteSpace(accCode)
                    && !string.Equals(st.AccCode, accCode, StringComparison.OrdinalIgnoreCase)) continue;

                var sec = await _db.Sectors.AsNoTracking()
                    .Where(x => x.Callsign == st.Callsign)
                    .Select(x => new { x.Id, x.Name, x.DocumentId, Titolo = x.Document!.Title })
                    .FirstOrDefaultAsync(ct);
                if (sec is null) continue;   // in catalogo ma mai proiettato: non è roba di questa pagina

                righe.Add(await RigaAsync(sec.Id, st.Callsign, sec.Name, st.AccCode, sec.DocumentId, sec.Titolo,
                    OrphanReason.NotListed, st.LastSeenUtc, ct));
            }
        }

        return righe.OrderBy(r => r.AccCode).ThenBy(r => r.Callsign).ToList();
    }

    public async Task<OrphanSectorRow?> GetOrphanAsync(int sectorId, DateTime? sogliaTimbro, CancellationToken ct = default)
    {
        var s = await _db.Sectors.AsNoTracking()
            .Where(x => x.Id == sectorId && x.IsProjected)
            .Select(x => new { x.Id, x.Callsign, x.Name, AccCode = x.Acc!.Code, x.DocumentId, x.IsActive, Titolo = x.Document!.Title })
            .FirstOrDefaultAsync(ct);
        if (s is null) return null;

        if (!s.IsActive)
            return await RigaAsync(s.Id, s.Callsign, s.Name, s.AccCode, s.DocumentId, s.Titolo,
                await InCatalogoAsync(s.Callsign, ct) ? OrphanReason.Hidden : OrphanReason.Gone, null, ct);

        // Attivo: è di questa pagina solo se la sorgente ha smesso di mandarlo.
        if (sogliaTimbro is not { } soglia) return null;
        var st = (await ListStaleCatalogRowsAsync(soglia, ct))
            .FirstOrDefault(x => string.Equals(x.Callsign, s.Callsign, StringComparison.OrdinalIgnoreCase));
        if (st is null) return null;

        return await RigaAsync(s.Id, s.Callsign, s.Name, s.AccCode, s.DocumentId, s.Titolo,
            OrphanReason.NotListed, st.LastSeenUtc, ct);
    }

    /// <summary>Una riga completa: i documenti che la raccontano e chi ne impedisce la rimozione.</summary>
    private async Task<OrphanSectorRow> RigaAsync(
        int id, string callsign, string nome, string accCode, int? docId, string? titolo,
        OrphanReason motivo, DateTime? ultimoTimbro, CancellationToken ct) =>
        new(id, callsign, nome, accCode, motivo, docId, titolo,
            await _impacts.FindDocumentsForSectorAsync(callsign, accCode, ct),
            await BloccantiAsync(id, callsign, ct),
            ultimoTimbro);

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaleAirportRow>> ListStaleAirportsAsync(
        DateTime sogliaUtc, CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking()
            .Where(a => a.LastSeenAtUtc != null && a.LastSeenAtUtc < sogliaUtc)
            .OrderBy(a => a.Acc!.Code).ThenBy(a => a.Icao)
            .Select(a => new StaleAirportRow(
                a.Id, a.Icao, a.Name, a.Acc!.Code, a.LastSeenAtUtc,
                a.Sectors.Count, a.DocumentId != null))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaleCatalogRow>> ListStaleCatalogRowsAsync(
        DateTime sogliaUtc, CancellationToken ct = default)
    {
        var righe = new List<StaleCatalogRow>();

        righe.AddRange(await _db.AccSectors.AsNoTracking()
            .Where(x => !x.IsManual && !x.IsHidden && x.ImportedAtUtc != null && x.ImportedAtUtc < sogliaUtc)
            .Select(x => new StaleCatalogRow(x.ComposePosition, x.CenterId, x.ImportedAtUtc!.Value))
            .ToListAsync(ct));

        righe.AddRange(await _db.AirportSectors.AsNoTracking()
            .Where(x => !x.IsManual && !x.IsHidden && x.ImportedAtUtc != null && x.ImportedAtUtc < sogliaUtc)
            .Select(x => new StaleCatalogRow(x.ComposePosition, x.AccCode, x.ImportedAtUtc!.Value))
            .ToListAsync(ct));

        return righe;
    }

    /// <inheritdoc />
    public async Task<int> CountCatalogRowsAsync(CancellationToken ct = default) =>
        await _db.AccSectors.AsNoTracking().CountAsync(x => !x.IsManual && !x.IsHidden, ct)
        + await _db.AirportSectors.AsNoTracking().CountAsync(x => !x.IsManual && !x.IsHidden, ct);

    public async Task<string?> GetAccCodeAsync(int sectorId, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking().Where(s => s.Id == sectorId && s.Acc != null)
            .Select(s => s.Acc!.Code).FirstOrDefaultAsync(ct);
}
