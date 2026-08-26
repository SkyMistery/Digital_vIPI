using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.Infrastructure.Persistence;

/// <inheritdoc cref="IDeletionRepository"/>
public sealed class EfDeletionRepository : IDeletionRepository
{
    private readonly VipiDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IDocumentImpactRepository _impatti;

    /// <param name="impatti">Serve al solo reverse-lookup delle aree regolamentate: «chi la cita» è una
    /// domanda che sa rispondere quel repository, e riscriverla qui sarebbe la seconda versione della stessa
    /// query.</param>
    public EfDeletionRepository(VipiDbContext db, IUnitOfWork uow, IDocumentImpactRepository impatti)
    {
        _db = db;
        _uow = uow;
        _impatti = impatti;
    }

    // ── Fatti ────────────────────────────────────────────────────────────────────────────────────────

    public async Task<SectorFacts?> SectorFactsAsync(int sectorId, CancellationToken ct = default)
    {
        var s = await _db.Sectors.AsNoTracking()
            .Where(x => x.Id == sectorId)
            .Select(x => new
            {
                x.Id, x.Callsign, x.Name, AccCode = x.Acc!.Code, x.Type, x.Kind,
                x.AirportId, x.AirportIcao, x.ParentSectorId, x.IsProjected, x.DocumentId, x.ImportedAtUtc,
            })
            .FirstOrDefaultAsync(ct);
        if (s is null) return null;

        var padre = s.ParentSectorId is int pid
            ? await _db.Sectors.AsNoTracking().Where(x => x.Id == pid).Select(x => x.Callsign).FirstOrDefaultAsync(ct)
            : null;

        var figli = await _db.Sectors.AsNoTracking()
            .Where(x => x.ParentSectorId == sectorId)
            .OrderBy(x => x.Callsign)
            .Select(x => new ChildFacts(x.Id, x.Callsign))
            .ToListAsync(ct);

        // Il timbro che conta è quello della riga di CATALOGO: il `Sector` è una proiezione, e il suo
        // ImportedAtUtc dice quando è nato lo specchio, non quando la sorgente ha parlato l'ultima volta.
        var (timbro, manuale) = await TimbroDiCatalogoAsync(s.Callsign, s.ImportedAtUtc, ct);

        var documenti = await DocumentiCheLoCitanoAsync(sectorId, s.DocumentId, ct);
        var accordi = await AccordiAsync(sectorId, ct);

        return new SectorFacts(
            s.Id, s.Callsign, s.Name, s.AccCode, s.Type, s.Kind,
            s.AirportId, s.AirportIcao, s.ParentSectorId, padre,
            s.IsProjected, manuale, timbro, figli,
            await FigliDiCatalogoAsync(s.Callsign, ct), documenti, accordi);
    }

    /// <summary>
    /// Chi si appende a questo callsign nel <b>catalogo</b>: righe ACC, posizioni d'aeroporto e gli
    /// <b>aeroporti</b> stessi, che dell'albero sono le foglie. È l'insieme che la proiezione rileggerà al
    /// prossimo sync — e se ci trova un padre sparito, il figlio diventa radice.
    /// </summary>
    private async Task<IReadOnlyList<CatalogChildFacts>> FigliDiCatalogoAsync(string callsign, CancellationToken ct)
    {
        var righe = new List<CatalogChildFacts>();
        righe.AddRange(await _db.AccSectors.AsNoTracking()
            .Where(x => x.ParentCallsign == callsign)
            .Select(x => new CatalogChildFacts(x.ComposePosition, CatalogChildKind.AccSector)).ToListAsync(ct));
        righe.AddRange(await _db.AirportSectors.AsNoTracking()
            .Where(x => x.ParentCallsign == callsign)
            .Select(x => new CatalogChildFacts(x.ComposePosition, CatalogChildKind.AirportSector)).ToListAsync(ct));
        righe.AddRange(await _db.Airports.AsNoTracking()
            .Where(x => x.ParentCallsign == callsign)
            .Select(x => new CatalogChildFacts(x.Icao, CatalogChildKind.Airport)).ToListAsync(ct));
        return righe;
    }

    public async Task<int?> SectorIdByCallsignAsync(string callsign, CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(x => x.Callsign == callsign)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<AirportFacts?> AirportFactsAsync(int airportId, CancellationToken ct = default)
    {
        var a = await _db.Airports.AsNoTracking()
            .Where(x => x.Id == airportId)
            .Select(x => new
            {
                x.Id, x.Icao, x.Name, AccCode = x.Acc!.Code, x.LastSeenAtUtc, x.DocumentId,
                Titolo = x.Document!.Title,
            })
            .FirstOrDefaultAsync(ct);
        if (a is null) return null;

        var ids = await _db.Sectors.AsNoTracking()
            .Where(x => x.AirportId == airportId)
            .OrderBy(x => x.CoverageOrder).ThenBy(x => x.Callsign)
            .Select(x => x.Id)
            .ToListAsync(ct);

        // Un settore per volta: sono al massimo una manciata per scalo, e passare dalla stessa lettura
        // significa che le protezioni del singolo settore valgono identiche dentro la cascata.
        var settori = new List<SectorFacts>();
        foreach (var id in ids)
            if (await SectorFactsAsync(id, ct) is { } f) settori.Add(f);

        return new AirportFacts(a.Id, a.Icao, a.Name, a.AccCode, a.LastSeenAtUtc, a.DocumentId, a.Titolo, settori);
    }

    public async Task<AccFacts?> AccFactsAsync(string accCode, CancellationToken ct = default)
    {
        var acc = await _db.Accs.AsNoTracking()
            .Where(x => x.Code == accCode)
            .Select(x => new { x.Id, x.Code, x.Name, x.ImportedAtUtc })
            .FirstOrDefaultAsync(ct);
        if (acc is null) return null;

        return new AccFacts(acc.Code, acc.Name, acc.ImportedAtUtc,
            await _db.Sectors.CountAsync(s => s.AccId == acc.Id, ct),
            await _db.Airports.CountAsync(a => a.AccId == acc.Id, ct));
    }

    public async Task<DocumentFacts?> DocumentFactsAsync(int documentId, CancellationToken ct = default)
    {
        var d = await _db.Documents.AsNoTracking()
            .Where(x => x.Id == documentId)
            .Select(x => new { x.Id, x.Title, x.Type, x.Status })
            .FirstOrDefaultAsync(ct);
        if (d is null) return null;

        var settori = await _db.Sectors.AsNoTracking()
            .Where(x => x.DocumentId == documentId).OrderBy(x => x.Callsign)
            .Select(x => x.Callsign).ToListAsync(ct);
        var aeroporto = await _db.Airports.AsNoTracking()
            .Where(x => x.DocumentId == documentId).Select(x => x.Icao).FirstOrDefaultAsync(ct);

        return new DocumentFacts(d.Id, d.Title, d.Type, d.Status == DocumentStatus.Published,
            Release: 0, settori, aeroporto);
    }

    public async Task<NeighbourFacts?> NeighbourFactsAsync(int candidateId, CancellationToken ct = default)
    {
        var n = await _db.NeighbourCandidates.AsNoTracking()
            .Where(x => x.Id == candidateId)
            .Select(x => new
            {
                x.Id, x.HomeAccCode, x.ForeignAccCode, x.ForeignAccName, x.ForeignRootCallsign,
                x.Status, x.VloaDocumentId,
            })
            .FirstOrDefaultAsync(ct);
        if (n is null) return null;

        var titolo = n.VloaDocumentId is int id
            ? await _db.Documents.AsNoTracking().Where(d => d.Id == id).Select(d => d.Title).FirstOrDefaultAsync(ct)
            : null;

        return new NeighbourFacts(
            n.Id, n.HomeAccCode, n.ForeignAccCode, n.ForeignAccName, n.ForeignRootCallsign,
            Confermato: n.Status == NeighbourCandidateStatus.Confirmed,
            n.VloaDocumentId, titolo,
            SettoreEsteroPresente: await _db.Sectors.AnyAsync(s => s.Callsign == n.ForeignRootCallsign, ct));
    }

    public async Task<AreaFacts?> AreaFactsAsync(string ivaoId, CancellationToken ct = default)
    {
        var a = await _db.SpecialAreas.AsNoTracking()
            .Where(x => x.IvaoId == ivaoId)
            .Select(x => new { x.IvaoId, x.Name })
            .FirstOrDefaultAsync(ct);
        if (a is null) return null;

        return new AreaFacts(
            a.IvaoId, a.Name ?? a.IvaoId,
            await _db.SpecialAreaCenters.CountAsync(l => l.IvaoId == ivaoId, ct),
            (await _impatti.FindDocumentsForSpecialAreaAsync(ivaoId, ct)).Select(d => d.Title).ToList());
    }

    public Task<int> ReleaseCountAsync(ReleaseTargetType tipo, string chiave, CancellationToken ct = default) =>
        _db.DocReleases.AsNoTracking().CountAsync(r => r.TargetType == tipo && r.TargetKey == chiave, ct);

    /// <summary>
    /// Il timbro della riga di catalogo che porta questo callsign, e se quella riga è stata aggiunta a mano.
    /// Con doppioni fra i due cataloghi vince il timbro più <b>recente</b>: basta una conferma per dire che
    /// la sorgente lo manda ancora.
    /// </summary>
    private async Task<(DateTime? Timbro, bool Manuale)> TimbroDiCatalogoAsync(
        string callsign, DateTime? fallback, CancellationToken ct)
    {
        var acc = await _db.AccSectors.AsNoTracking()
            .Where(x => x.ComposePosition == callsign)
            .Select(x => new { x.ImportedAtUtc, x.IsManual }).ToListAsync(ct);
        var apt = await _db.AirportSectors.AsNoTracking()
            .Where(x => x.ComposePosition == callsign)
            .Select(x => new { x.ImportedAtUtc, x.IsManual }).ToListAsync(ct);

        var righe = acc.Concat(apt).ToList();
        if (righe.Count == 0) return (fallback, false);

        var timbro = righe.Where(r => r.ImportedAtUtc is not null).Select(r => r.ImportedAtUtc).DefaultIfEmpty(null).Max();
        return (timbro ?? fallback, righe.All(r => r.IsManual));
    }

    /// <summary>
    /// I documenti che citano il settore, in tutte e tre le vesti: <b>ancorato</b> (il documento è appeso a
    /// questo settore), <b>parte</b> (vLOA), <b>blocco</b> (ambito o estremo di un da→a). Per ciascuno dice
    /// se dopo la rimozione resterebbe agganciato a qualcos'altro — la differenza fra sganciare e fermarsi.
    /// </summary>
    private async Task<IReadOnlyList<DocRefFacts>> DocumentiCheLoCitanoAsync(
        int sectorId, int? ancorato, CancellationToken ct)
    {
        var parti = await _db.DocumentParties.AsNoTracking()
            .Where(p => p.SectorId == sectorId)
            .Select(p => new { p.Id, p.DocumentId })
            .ToListAsync(ct);

        var blocchi = await _db.ContentBlocks.AsNoTracking()
            .Where(b => b.ScopeSectorId == sectorId || b.FromSectorId == sectorId || b.ToSectorId == sectorId)
            .Select(b => new
            {
                b.Id,
                DocumentId = b.DocumentVersion!.DocumentId,
                Sezione = b.Section!.Title,
                Scope = b.ScopeSectorId == sectorId,
                Estremo = b.FromSectorId == sectorId || b.ToSectorId == sectorId,
            })
            .ToListAsync(ct);

        var ids = new HashSet<int>();
        if (ancorato is int a) ids.Add(a);
        foreach (var p in parti) ids.Add(p.DocumentId);
        foreach (var b in blocchi) ids.Add(b.DocumentId);
        if (ids.Count == 0) return Array.Empty<DocRefFacts>();

        var titoli = await _db.Documents.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, d.Title })
            .ToDictionaryAsync(x => x.Id, x => x.Title, ct);

        var righe = new List<DocRefFacts>();
        foreach (var id in ids.OrderBy(x => x))
        {
            // Un documento resta agganciato se qualcos'ALTRO lo indica: un altro settore, l'aeroporto (per la
            // vIPI di scalo il legame autoritativo è quello), o un'altra parte della vLOA.
            var altroSettore = await _db.Sectors.AnyAsync(s => s.DocumentId == id && s.Id != sectorId, ct);
            var aeroporto = await _db.Airports.AnyAsync(x => x.DocumentId == id, ct);
            var altraParte = await _db.DocumentParties.AnyAsync(p => p.DocumentId == id && p.SectorId != sectorId, ct);

            righe.Add(new DocRefFacts(
                id, titoli.TryGetValue(id, out var t) ? t : $"documento {id}",
                AncoraQui: ancorato == id,
                Parti: parti.Where(p => p.DocumentId == id).Select(p => p.Id).ToList(),
                Blocchi: blocchi.Where(b => b.DocumentId == id)
                    .Select(b => new BlockRefFacts(b.Id, b.Sezione, b.Scope, b.Estremo)).ToList(),
                RestaAncorato: altroSettore || aeroporto || altraParte));
        }
        return righe;
    }

    private async Task<IReadOnlyList<AgreementFacts>> AccordiAsync(int sectorId, CancellationToken ct)
    {
        var righe = await _db.CoordinationAgreements.AsNoTracking()
            .Where(x => x.SideASectorId == sectorId || x.SideBSectorId == sectorId)
            .Select(x => new
            {
                x.Id,
                A = x.SideASector!.Callsign,
                B = x.SideBSector!.Callsign,
                Acc = x.OwnerAcc!.Code,
            })
            .ToListAsync(ct);

        return righe
            .Select(x => new AgreementFacts(x.Id, $"{x.A} ↔ {x.B}",
                $"/services/vsop/admin/transfers?acc={x.Acc.ToLowerInvariant()}"))
            .ToList();
    }

    // ── Esecuzione ───────────────────────────────────────────────────────────────────────────────────

    public Task ApplyAsync(DeletionActions azioni, int actorUserId, CancellationToken ct = default) =>
        _uow.ExecuteInTransactionAsync(token => EseguiAsync(azioni, actorUserId, token), ct);

    private async Task EseguiAsync(DeletionActions a, int actorUserId, CancellationToken ct)
    {
        // 1) I figli al nonno, PRIMA del DELETE: la FK sul padre è Restrict e non perdona.
        if (a.FigliDaRiappendere.Count > 0)
        {
            var figli = await _db.Sectors.Where(s => a.FigliDaRiappendere.Contains(s.Id)).ToListAsync(ct);
            foreach (var f in figli) f.ParentSectorId = a.NuovoPadreDeiFigli;
        }

        // 2) I riferimenti: le parti spariscono, i blocchi falsi spariscono, quelli senza ambito si sganciano.
        if (a.PartiDaEliminare.Count > 0)
        {
            var parti = await _db.DocumentParties.Where(p => a.PartiDaEliminare.Contains(p.Id)).ToListAsync(ct);
            _db.DocumentParties.RemoveRange(parti);
        }
        if (a.BlocchiDaEliminare.Count > 0)
        {
            var blocchi = await _db.ContentBlocks.Where(b => a.BlocchiDaEliminare.Contains(b.Id)).ToListAsync(ct);
            _db.ContentBlocks.RemoveRange(blocchi);
        }
        if (a.BlocchiDaSganciare.Count > 0)
        {
            var blocchi = await _db.ContentBlocks.Where(b => a.BlocchiDaSganciare.Contains(b.Id)).ToListAsync(ct);
            foreach (var b in blocchi) b.ScopeSectorId = null;
        }

        // 3) Il riaggancio nel CATALOGO: senza, il prossimo sync rileggerebbe un padre sparito e farebbe
        //    dei figli altrettante radici — la promessa «i figli passano al nonno» durerebbe una notte.
        foreach (var r in a.RiaggancioDiCatalogo)
        {
            switch (r.Dove)
            {
                case CatalogChildKind.AccSector:
                    foreach (var x in await _db.AccSectors.Where(x => x.ComposePosition == r.Figlio).ToListAsync(ct))
                        x.ParentCallsign = r.NuovoPadre;
                    break;
                case CatalogChildKind.AirportSector:
                    foreach (var x in await _db.AirportSectors.Where(x => x.ComposePosition == r.Figlio).ToListAsync(ct))
                        x.ParentCallsign = r.NuovoPadre;
                    break;
                default:
                    foreach (var x in await _db.Airports.Where(x => x.Icao == r.Figlio).ToListAsync(ct))
                        x.ParentCallsign = r.NuovoPadre;
                    break;
            }
        }

        // 4) Le righe di catalogo che rimanderebbero in vita il settore al primo sync.
        if (a.CallsignDiCatalogoDaTogliere.Count > 0)
        {
            var cs = a.CallsignDiCatalogoDaTogliere;
            var acc = await _db.AccSectors.Where(x => cs.Contains(x.ComposePosition)).ToListAsync(ct);
            if (acc.Count > 0) _db.AccSectors.RemoveRange(acc);
            var apt = await _db.AirportSectors.Where(x => cs.Contains(x.ComposePosition)).ToListAsync(ct);
            if (apt.Count > 0) _db.AirportSectors.RemoveRange(apt);
        }

        // 5) L'audit va scritto PRIMA della cancellazione: dopo, il nome non è più leggibile e resterebbe un
        //    registro che dice «eliminato il settore 7». Il nome accanto all'Id è tutto ciò che, fra sei mesi,
        //    distingue una pulizia da un incidente.
        var settori = a.SettoriDaEliminare.Count > 0
            ? await _db.Sectors.Where(s => a.SettoriDaEliminare.Contains(s.Id)).ToListAsync(ct)
            : new List<Domain.Entities.Sector>();
        foreach (var s in settori)
            AuditScribe.Write(_db, actorUserId, AuditAction.Delete, "Sector", s.Id.ToString(),
                new { s.Callsign, s.Name, s.Type, s.Kind, s.AirportIcao });

        if (a.AeroportoDaEliminare is int aptId)
        {
            var apt = await _db.Airports.FirstOrDefaultAsync(x => x.Id == aptId, ct);
            if (apt is not null)
                AuditScribe.Write(_db, actorUserId, AuditAction.Delete, "Airport", apt.Id.ToString(),
                    new { apt.Icao, apt.Name, Settori = settori.Count });
        }

        if (a.AccDaEliminare is { } accCode)
        {
            var acc = await _db.Accs.FirstOrDefaultAsync(x => x.Code == accCode, ct);
            if (acc is not null)
                AuditScribe.Write(_db, actorUserId, AuditAction.Delete, "Acc", acc.Code, new { acc.Name });
        }

        if (a.CandidatoDaEliminare is int candId)
        {
            var cand = await _db.NeighbourCandidates.FirstOrDefaultAsync(x => x.Id == candId, ct);
            if (cand is not null)
            {
                AuditScribe.Write(_db, actorUserId, AuditAction.Delete, "NeighbourCandidate", cand.Id.ToString(),
                    new { cand.HomeAccCode, cand.ForeignAccCode, cand.ForeignAccName, cand.Status });
                _db.NeighbourCandidates.Remove(cand);
            }
        }

        if (a.AreaDaEliminare is { } areaId)
        {
            var area = await _db.SpecialAreas.FirstOrDefaultAsync(x => x.IvaoId == areaId, ct);
            if (area is not null)
            {
                AuditScribe.Write(_db, actorUserId, AuditAction.Delete, "SpecialArea", area.IvaoId,
                    new { area.Name, area.Type });
                // I legami con gli enti vanno via con lei: restare sarebbero righe che indicano un'area
                // inesistente, e nessuna pagina saprebbe più toglierle.
                var legami = await _db.SpecialAreaCenters.Where(l => l.IvaoId == areaId).ToListAsync(ct);
                if (legami.Count > 0) _db.SpecialAreaCenters.RemoveRange(legami);
                _db.SpecialAreas.Remove(area);
            }
        }

        // 6) I DELETE veri, dal figlio al padre.
        if (settori.Count > 0) _db.Sectors.RemoveRange(settori);
        if (a.AeroportoDaEliminare is int id2)
        {
            var apt = await _db.Airports.FirstOrDefaultAsync(x => x.Id == id2, ct);
            if (apt is not null) _db.Airports.Remove(apt);
        }
        if (a.AccDaEliminare is { } code2)
        {
            var acc = await _db.Accs.FirstOrDefaultAsync(x => x.Code == code2, ct);
            if (acc is not null) _db.Accs.Remove(acc);
        }

        await _db.SaveChangesAsync(ct);
    }
}
