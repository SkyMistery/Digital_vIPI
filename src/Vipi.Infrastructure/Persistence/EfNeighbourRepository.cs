using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="INeighbourRepository"/>. Staging delle coppie ACC confinanti candidate a vLOA;
/// alla conferma materializza (upsert) ACC + settore radice esteri e crea il Document vLOA bilaterale, idempotente.
/// </summary>
public sealed class EfNeighbourRepository : INeighbourRepository
{
    private readonly VipiDbContext _db;
    private readonly IAiracService _airac;
    private readonly Vipi.Application.Airspace.ISectorShapeResolver? _forme;

    /// <param name="forme">
    /// La porta unica per la forma di un settore (carta refactor 15). ⚠️ Facoltativa: senza, i confinanti si
    /// calcolano sulla colonna del catalogo, che è il comportamento di prima — serve ai test che montano
    /// questo repository da sé.
    /// </param>
    public EfNeighbourRepository(
        VipiDbContext db, IAiracService airac, Vipi.Application.Airspace.ISectorShapeResolver? forme = null)
    {
        _db = db;
        _airac = airac;
        _forme = forme;
    }

    public async Task<IReadOnlyList<DomesticSectorPoly>> ListDomesticSectorPolygonsAsync(CancellationToken ct = default)
    {
        // I settori domestici visibili. ⚠️ Il filtro resta sulla colonna — «ha una forma di qualche tipo» —
        // perché un settore senza niente in colonna non ha nemmeno un aggancio: gli agganci si fanno da una
        // pagina che elenca settori esistenti.
        var righe = await _db.AccSectors.AsNoTracking()
            .Where(s => s.RegionMapPolygon != null && !s.IsHidden && !s.Acc!.IsHidden && !s.Acc!.IsForeign)
            .Select(s => new { s.CenterId, s.ComposePosition, s.RegionMapPolygon })
            .ToListAsync(ct);

        if (_forme is null)
            return righe.Select(r => new DomesticSectorPoly(r.CenterId, r.ComposePosition, r.RegionMapPolygon!)).ToList();

        // ⚠️ La forma è quella che si vede nei documenti, non la colonna: un CTR agganciato al suo spazio
        // dell'AIP confina con chi tocca le SUE zone. Prima disegnava un confine e ne confrontava un altro.
        var forme = await _forme.ResolveAsync(righe.Select(r => r.ComposePosition).ToList(), ct);

        return righe.Select(r =>
        {
            var pezzi = forme.TryGetValue(r.ComposePosition, out var f)
                ? f.Parts.Select(x => x.PolygonJson).ToList()
                : new List<string> { r.RegionMapPolygon! };
            return new DomesticSectorPoly(r.CenterId, r.ComposePosition, pezzi);
        }).ToList();
    }

    public async Task<IReadOnlyList<string>> ListDomesticAccCodesAsync(CancellationToken ct = default) =>
        await _db.Accs.AsNoTracking().Where(a => !a.IsForeign).Select(a => a.Code).ToListAsync(ct);

    public async Task<IReadOnlyList<Vipi.Application.Content.ForeignAccData>> ListForeignAccDataAsync(
        CancellationToken ct = default)
    {
        var accs = await _db.Accs.AsNoTracking().Where(a => a.IsForeign)
            .Select(a => new { a.Code, a.Name, a.CountryPrefix })
            .ToListAsync(ct);
        if (accs.Count == 0) return Array.Empty<Vipi.Application.Content.ForeignAccData>();

        var codici = accs.Select(a => a.Code).ToList();
        var settori = await _db.AccSectors.AsNoTracking()
            .Where(s => codici.Contains(s.CenterId) && s.RegionMapPolygon != null)
            .Select(s => new
            {
                s.CenterId, s.ComposePosition, s.Position, s.MiddleIdentifier, s.Frequency,
                s.RegionMapPolygon, s.AtcCallsign, s.LowerLimit, s.UpperLimit,
            })
            .ToListAsync(ct);

        var perAcc = settori.GroupBy(s => s.CenterId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        return accs.Select(a => new Vipi.Application.Content.ForeignAccData(
            a.Code, a.Name, a.CountryPrefix,
            (perAcc.TryGetValue(a.Code, out var righe) ? righe : new())
                .Select(s => new SourceSubcenter(
                    s.ComposePosition, s.CenterId, s.Position, s.MiddleIdentifier, s.Frequency,
                    s.RegionMapPolygon, s.AtcCallsign, s.LowerLimit, s.UpperLimit))
                .ToList()))
            .ToList();
    }

    public async Task PersistForeignCatalogAsync(IReadOnlyList<ForeignAccImport> accs, bool manuale = false, CancellationToken ct = default)
    {
        if (accs.Count == 0) return;
        var now = DateTime.UtcNow;

        var existingAccs = await _db.Accs.ToDictionaryAsync(a => a.Code, StringComparer.OrdinalIgnoreCase, ct);
        // Upsert degli ACC esteri (flag IsForeign per escluderli dalle basi domestiche).
        foreach (var fa in accs)
        {
            var code = fa.Code.Trim().ToUpperInvariant();
            if (code.Length == 0) continue;
            if (existingAccs.TryGetValue(code, out var acc))
            {
                acc.Name = fa.Name;
                acc.IsForeign = true;
                acc.ImportedAtUtc = now;
            }
            else
            {
                acc = Acc.NewForeign(code, fa.Name);
                acc.ImportedAtUtc = now;
                _db.Accs.Add(acc);
                existingAccs[code] = acc;
            }
        }
        await _db.SaveChangesAsync(ct);

        // Upsert dei subcenter esteri come AccSector, preservando ParentCallsign/IsHidden impostati dall'admin.
        var existingSubs = await _db.AccSectors
            .ToDictionaryAsync(s => s.ComposePosition, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var fa in accs)
        {
            var center = fa.Code.Trim().ToUpperInvariant();
            foreach (var sub in fa.Subcenters)
            {
                var compose = sub.ComposePosition.Trim().ToUpperInvariant();
                if (compose.Length == 0) continue;
                if (existingSubs.TryGetValue(compose, out var row))
                {
                    row.IvaoId ??= sub.IvaoId;   // backfill: la riga c'era prima che l'identità esistesse
                    row.CenterId = center;
                    row.Position = sub.Position;
                    row.MiddleIdentifier = sub.MiddleIdentifier;
                    row.AtcCallsign = sub.AtcCallsign;
                    row.Frequency = sub.Frequency;
                    // Solo una shape VERA sovrascrive: l'assenza non e' un ordine di cancellare.
                    if (!PolygonGeometry.IsEmptyShape(sub.RegionMapPolygon)) row.RegionMapPolygon = sub.RegionMapPolygon;
                    if (sub.LowerLimit is not null) row.LowerLimit = sub.LowerLimit;
                    if (sub.UpperLimit is not null) row.UpperLimit = sub.UpperLimit;
                    // ParentCallsign / IsHidden: comando dell'admin, NON sovrascritti dall'import.
                    row.ImportedAtUtc = now;
                }
                else
                {
                    var added = new AccSector
                    {
                        // Sui confinanti veri l'id c'è (vengono da /v2/centers/{ACC}/subcenters); resta null
                        // solo sulle righe sintetiche di ForeignSectorResolver, che è il senso del null.
                        IvaoId = sub.IvaoId,
                        ComposePosition = compose,
                        CenterId = center,
                        Position = sub.Position,
                        MiddleIdentifier = sub.MiddleIdentifier,
                        AtcCallsign = sub.AtcCallsign,
                        Frequency = sub.Frequency,
                        RegionMapPolygon = PolygonGeometry.IsEmptyShape(sub.RegionMapPolygon) ? null : sub.RegionMapPolygon,
                        LowerLimit = sub.LowerLimit,
                        UpperLimit = sub.UpperLimit,
                        IsHidden = false,
                        ImportedAtUtc = now,
                        IsManual = manuale,
                    };
                    _db.AccSectors.Add(added);
                    existingSubs[compose] = added;
                }
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(int Created, int Updated)> UpsertCandidatesAsync(IReadOnlyList<NeighbourCandidateUpsert> items, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        int created = 0, updated = 0;

        var existing = await _db.NeighbourCandidates.ToListAsync(ct);
        var byKey = existing.ToDictionary(
            c => (c.HomeAccCode.ToUpperInvariant(), c.ForeignAccCode.ToUpperInvariant()));

        foreach (var it in items)
        {
            var key = (it.HomeAccCode.ToUpperInvariant(), it.ForeignAccCode.ToUpperInvariant());
            if (byKey.TryGetValue(key, out var row))
            {
                // Non toccare le righe già decise dall'admin (Confirmed/Rejected): il calcolo aggiorna solo le Pending.
                if (row.Status != NeighbourCandidateStatus.Pending) continue;
                row.ForeignAccName = it.ForeignAccName;
                row.CountryId = it.CountryId;
                row.ForeignRootCallsign = it.ForeignRootCallsign;
                row.MinDistanceNm = it.MinDistanceNm;
                row.AdjacentSectorCount = it.AdjacentSectorCount;
                row.AdjacentHomeCallsigns = SerializeCallsigns(it.AdjacentHomeCallsigns) ?? row.AdjacentHomeCallsigns;
                row.AdjacentForeignCallsigns = SerializeCallsigns(it.AdjacentForeignCallsigns) ?? row.AdjacentForeignCallsigns;
                row.RegionMapPolygon = it.RegionMapPolygon ?? row.RegionMapPolygon;   // preserva un poligono già incollato
                row.UpdatedUtc = now;
                updated++;
            }
            else
            {
                _db.NeighbourCandidates.Add(new NeighbourCandidate
                {
                    HomeAccCode = it.HomeAccCode.ToUpperInvariant(),
                    ForeignAccCode = it.ForeignAccCode.ToUpperInvariant(),
                    ForeignAccName = it.ForeignAccName,
                    CountryId = it.CountryId,
                    ForeignRootCallsign = it.ForeignRootCallsign.ToUpperInvariant(),
                    RegionMapPolygon = it.RegionMapPolygon,
                    MinDistanceNm = it.MinDistanceNm,
                    AdjacentSectorCount = it.AdjacentSectorCount,
                    AdjacentHomeCallsigns = SerializeCallsigns(it.AdjacentHomeCallsigns),
                    AdjacentForeignCallsigns = SerializeCallsigns(it.AdjacentForeignCallsigns),
                    Status = NeighbourCandidateStatus.Pending,
                    CreatedUtc = now,
                });
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (created, updated);
    }

    public async Task<IReadOnlyList<NeighbourCandidate>> ListCandidatesAsync(CancellationToken ct = default) =>
        await _db.NeighbourCandidates.AsNoTracking()
            .OrderBy(c => c.HomeAccCode).ThenBy(c => c.ForeignAccCode)
            .ToListAsync(ct);

    public Task<NeighbourCandidate?> GetAsync(int id, CancellationToken ct = default) =>
        _db.NeighbourCandidates.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task SetStatusAsync(int id, NeighbourCandidateStatus status, CancellationToken ct = default)
    {
        var c = await _db.NeighbourCandidates.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Candidato id {id} inesistente.");
        c.Status = status;
        c.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetPolygonAsync(int id, string? regionMapPolygon, CancellationToken ct = default)
    {
        var c = await _db.NeighbourCandidates.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException($"Candidato id {id} inesistente.");
        c.RegionMapPolygon = regionMapPolygon;
        c.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> AddManualAsync(NeighbourCandidateUpsert item, CancellationToken ct = default)
    {
        var home = item.HomeAccCode.ToUpperInvariant();
        var foreign = item.ForeignAccCode.ToUpperInvariant();
        var dup = await _db.NeighbourCandidates
            .FirstOrDefaultAsync(c => c.HomeAccCode == home && c.ForeignAccCode == foreign, ct);
        if (dup is not null)
            throw new InvalidOperationException(Lingua($"Coppia {home}↔{foreign} già presente.", $"The pair {home}↔{foreign} is already there."));

        var row = new NeighbourCandidate
        {
            HomeAccCode = home,
            ForeignAccCode = foreign,
            ForeignAccName = item.ForeignAccName,
            CountryId = item.CountryId,
            ForeignRootCallsign = item.ForeignRootCallsign.ToUpperInvariant(),
            RegionMapPolygon = item.RegionMapPolygon,
            MinDistanceNm = null,
            AdjacentSectorCount = 0,
            Status = NeighbourCandidateStatus.Pending,
            CreatedUtc = DateTime.UtcNow,
        };
        _db.NeighbourCandidates.Add(row);
        await _db.SaveChangesAsync(ct);
        return row.Id;
    }

    public async Task<SectorOwner?> FindSectorOwnerAsync(string callsign, CancellationToken ct = default)
    {
        var cs = (callsign ?? "").Trim();
        if (cs.Length == 0) return null;
        var acc = await _db.AccSectors.AsNoTracking()
            .Where(s => s.ComposePosition == cs)
            .Select(s => new SectorOwner(s.CenterId, s.IsHidden))
            .FirstOrDefaultAsync(ct);
        if (acc is not null) return acc;
        return await _db.AirportSectors.AsNoTracking()
            .Where(s => s.ComposePosition == cs)
            .Select(s => new SectorOwner(s.AccCode, s.IsHidden))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> MaterializeAndCreateVloaAsync(int candidateId, CancellationToken ct = default)
    {
        var cand = await _db.NeighbourCandidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct)
                   ?? throw new InvalidOperationException($"Candidato id {candidateId} inesistente.");

        // Se già generata, ritorna (idempotenza).
        if (cand.VloaDocumentId is int existingDoc &&
            await _db.Documents.AnyAsync(d => d.Id == existingDoc, ct))
            return existingDoc;

        // 1) Settore radice domestico (Home) dell'ACC italiano: radice dell'albero ACC, o il primo per copertura.
        var home = await _db.Sectors
            .Where(s => s.Acc!.Code == cand.HomeAccCode && s.Kind == SectorKind.Acc)
            .OrderBy(s => s.ParentSectorId == null ? 0 : 1)
            .ThenBy(s => s.CoverageOrder)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(Lingua(
                $"Nessun settore operativo per l'ACC {cand.HomeAccCode}: proietta i settori (pagina ACC) prima di generare la vLOA.",
                $"No operational sector for ACC {cand.HomeAccCode}: project the sectors (ACC page) before generating the vLOA."));

        // 2) Materializza ACC estero (upsert per Code).
        var fCode = cand.ForeignAccCode.ToUpperInvariant();
        var foreignAcc = await _db.Accs.FirstOrDefaultAsync(a => a.Code == fCode, ct);
        if (foreignAcc is null)
        {
            foreignAcc = Acc.NewForeign(fCode, cand.ForeignAccName);
            _db.Accs.Add(foreignAcc);
            await _db.SaveChangesAsync(ct);
        }
        else if (!foreignAcc.IsForeign) { foreignAcc.IsForeign = true; await _db.SaveChangesAsync(ct); }

        // 3) Settore radice estero: preferisci quello PROIETTATO dal catalogo (subcenter senza padre, da import
        //    confinanti); fallback = materializza a mano il ForeignRootCallsign se non ci sono settori proiettati.
        var foreign = await _db.Sectors
            .Where(s => s.Acc!.Code == fCode && s.Kind == SectorKind.Acc)
            .OrderBy(s => s.ParentSectorId == null ? 0 : 1)
            .ThenBy(s => s.CoverageOrder)
            .FirstOrDefaultAsync(ct);
        if (foreign is null)
        {
            foreign = new Sector
            {
                AccId = foreignAcc.Id,
                Callsign = cand.ForeignRootCallsign.ToUpperInvariant(),
                Type = SectorType.Ctr,
                Kind = SectorKind.Acc,
                Name = cand.ForeignAccName,
                CoverageOrder = 10,
                ImportedAtUtc = DateTime.UtcNow,
                IsActive = true,
            };
            _db.Sectors.Add(foreign);
            await _db.SaveChangesAsync(ct);
        }

        // 4) Idempotenza per COPPIA DI ACC: se esiste già una vLOA HomeAcc↔ForeignAcc, riusala.
        //
        // ⚠️ Per ACC e non per SETTORE, e la differenza è un difetto vero (trovato il 29 agosto 2026).
        // Questa generazione sceglie da sé il settore Home — la radice dell'albero, o il primo per copertura —
        // mentre «Nuovo documento» lascia scegliere QUALUNQUE settore d'area dell'ACC. Su una ACC con più
        // settori d'area bastava che una vLOA fosse nata dall'altra porta su un settore diverso: il confronto
        // per SectorId non la trovava, e nasceva la SECONDA vLOA sulla stessa coppia.
        //
        // Da lì in poi le due strade non si vedono più fra loro: `FindVloaIdByPairAsync` — che è come
        // l'editor e il pubblico trovano la vLOA di una coppia — fa `FirstOrDefault` per codice ACC e ne apre
        // una SENZA UN CRITERIO. L'altra resta invisibile pur potendo avere release pubblicate.
        //
        // La regola è «una vLOA per coppia di ACC» (la dichiara EditingService.CreateDocumentAsync, che con
        // questa lettura la impone già): le due porte ora fanno la stessa domanda.
        var already = await _db.Documents
            .Where(d => d.Type == DocumentType.Vloa
                && d.Parties.Any(p => p.Role == PartyRole.Home && p.Sector!.Acc!.Code == cand.HomeAccCode)
                && d.Parties.Any(p => p.Role == PartyRole.Neighbour && p.Sector!.Acc!.Code == fCode))
            .Select(d => d.Id)
            .FirstOrDefaultAsync(ct);
        if (already != 0)
        {
            cand.VloaDocumentId = already;
            await _db.SaveChangesAsync(ct);
            return already;
        }

        // 5) Crea il Document vLOA (skeleton pubblicato, contenuto editabile poi dal lato Home).
        var now = DateTime.UtcNow;
        var cycle = _airac.GetCycle(now);
        var doc = new Document
        {
            Type = DocumentType.Vloa,
            Title = $"vLOA — {cand.HomeAccCode} ↔ {fCode}",
            Language = Language.En,
            Status = DocumentStatus.Published,
            LastUpdatedUtc = now,
            LastUpdatedAiracCycle = cycle,
        };
        var ver = new DocumentVersion
        {
            Document = doc, VersionNumber = 1, Status = DocumentStatus.Published,
            CreatedByUserId = 0, CreatedUtc = now, AiracCycle = cycle, Note = "Generata da coppia ACC confinante",
        };
        doc.Versions.Add(ver);
        doc.Parties.Add(new DocumentParty { Document = doc, SectorId = home.Id, Role = PartyRole.Home });
        doc.Parties.Add(new DocumentParty { Document = doc, SectorId = foreign.Id, Role = PartyRole.Neighbour });
        _db.Documents.Add(doc);

        // Struttura OBBLIGATORIA della vLOA (sezioni mockup 3d): seedata alla generazione (contenuto placeholder).
        Seed.VloaStructureSeeder.Seed(_db, ver,
            Vipi.Application.Content.VloaSections.Canonical(cand.HomeAccCode, fCode, cand.ForeignAccName));

        await _db.SaveChangesAsync(ct);

        // ⚠️ `CurrentVersionId` NON si imposta: la vLOA nasce in bozza, e quel campo vuol dire «la versione
        // PUBBLICATA corrente». Lo scrive `PublishAsync`. Era l'altra delle due porte su quattro che lo
        // puntavano a una bozza — l'aeroporto e questa.
        cand.VloaDocumentId = doc.Id;
        cand.UpdatedUtc = now;
        await _db.SaveChangesAsync(ct);
        return doc.Id;
    }

    private static string? SerializeCallsigns(IReadOnlyList<string>? callsigns) =>
        callsigns is null ? null : JsonSerializer.Serialize(callsigns);
}
