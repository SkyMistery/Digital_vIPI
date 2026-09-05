using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Auth;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>Implementazione EF di <see cref="IStructureEditingRepository"/> (anagrafica ACC, ACC-scoped).</summary>
public sealed class EfStructureEditingRepository : IStructureEditingRepository
{
    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService? _authz;

    /// <param name="authz">
    /// Serve a una cosa sola: sapere <b>chi</b> sta cancellando, per il registro di audit. Opzionale perché
    /// le prove costruiscono il repository col solo contesto e non hanno un utente; in esercizio la DI lo
    /// passa sempre. ⚠️ Senza, la riga di audit si scrive lo stesso con attore <c>0</c>: il fatto va nel
    /// registro anche quando l'attore non è noto — un'eliminazione muta è peggio di una senza firma.
    /// </param>
    public EfStructureEditingRepository(VipiDbContext db, IEditAuthorizationService? authz = null)
    {
        _db = db;
        _authz = authz;
    }

    /// <summary>Chi sta agendo, per il registro. <c>0</c> = attore non noto (vedi il costruttore).</summary>
    private int Attore => _authz?.CurrentUserId ?? 0;

    public async Task<IReadOnlyList<AccRow>> ListAccsAsync(CancellationToken ct = default) =>
        await _db.Accs.AsNoTracking()
            .OrderBy(f => f.Code)
            .Select(f => new AccRow(f.Id, f.Code, f.Name, f.CountryPrefix, f.Sectors.Count))
            .ToListAsync(ct);

    public Task<bool> AccExistsAsync(string code, CancellationToken ct = default) =>
        _db.Accs.AnyAsync(f => f.Code == code, ct);

    public async Task<int> CreateAccAsync(string code, string name, string countryPrefix, CancellationToken ct = default)
    {
        var acc = new Acc { Code = code, Name = name, CountryPrefix = countryPrefix };
        _db.Accs.Add(acc);
        await _db.SaveChangesAsync(ct);
        return acc.Id;
    }

    public async Task DeleteAccAsync(string accCode, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        // I settori portano contenuto/documenti: vanno rimossi esplicitamente prima.
        if (await _db.Sectors.AnyAsync(s => s.AccId == fid, ct))
            throw new InvalidOperationException(Lingua("Impossibile eliminare la ACC: rimuovi prima i settori.", "The ACC cannot be deleted: remove its sectors first."));
        // Gli aeroporti (spesso auto-assegnati in blocco) seguono la ACC: FK Sector.AirportId è SetNull.
        var airports = await _db.Airports.Where(a => a.AccId == fid).ToListAsync(ct);
        if (airports.Count > 0) _db.Airports.RemoveRange(airports);
        var acc = await _db.Accs.FirstAsync(f => f.Id == fid, ct);
        // ⚠️ PRIMA della cancellazione: dopo, nome e codice non sono più leggibili e resterebbe un registro
        // che non dice che cosa è sparito. Stessa lezione di EliminaBozzaAsync, stesso vocabolario di
        // EfDeletionRepository — le due strade cancellano le stesse cose e devono raccontarle uguale.
        AuditScribe.Write(_db, Attore, AuditAction.Delete, "Acc", acc.Code,
            new { acc.Name, Aeroporti = airports.Count });
        _db.Accs.Remove(acc);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<StructureData?> LoadAsync(string accCode, CancellationToken ct = default)
    {
        var acc = await _db.Accs.AsNoTracking().FirstOrDefaultAsync(f => f.Code == accCode, ct);
        if (acc is null) return null;

        var airports = await _db.Airports.AsNoTracking().Where(a => a.AccId == acc.Id)
            .OrderBy(a => a.Icao)
            .Select(a => new AirportRow(a.Id, a.Icao, a.Name, a.Sectors.Count, a.FeaturedRank, a.IsHidden, a.ParentCallsign,
                a.HasMilitaryPresence, a.IsMilitaryOnly))
            .ToListAsync(ct);

        var sectors = await _db.Sectors.AsNoTracking().Where(s => s.AccId == acc.Id)
            .OrderBy(s => s.CoverageOrder).ThenBy(s => s.Callsign)
            .Select(s => new SectorRow(
                s.Id, s.Callsign, s.Type, s.Kind, s.Name, s.DefaultFrequency, s.CoverageOrder,
                s.ApproachKind, s.ParentSectorId, s.AirportId, s.AirportIcao, s.IsActive, s.DocumentId, s.IsPrimary, s.FeaturedRank))
            .ToListAsync(ct);

        // vLOA pubblicate della ACC (centro confinante = party Neighbour); ordine card = FeaturedRank poi titolo.
        var vloaDocs = await _db.Documents.AsNoTracking()
            .Where(d => d.Type == DocumentType.Vloa
                        && d.Status == DocumentStatus.Published
                        && d.Parties.Any(pa => pa.Role == PartyRole.Home && pa.Sector!.AccId == acc.Id))
            .Include(d => d.Parties).ThenInclude(pa => pa.Sector).ThenInclude(s => s!.Acc)
            .ToListAsync(ct);
        var vloas = vloaDocs
            .Select(d =>
            {
                var neigh = d.Parties.FirstOrDefault(pa => pa.Role == PartyRole.Neighbour)?.Sector;
                return new VloaRow(d.Id, d.Title, neigh?.Name, d.FeaturedRank, neigh?.Acc?.Code);
            })
            .OrderBy(v => v.FeaturedRank ?? int.MaxValue).ThenBy(v => v.Title)
            .ToList();

        return new StructureData
        {
            AccId = acc.Id, AccCode = acc.Code, AccName = acc.Name,
            Airports = airports, Sectors = sectors, Vloas = vloas,
        };
    }

    public async Task SetFeaturedAirportsAsync(string accCode, IReadOnlyList<int> orderedAirportIds, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var airports = await _db.Airports.Where(a => a.AccId == fid).ToListAsync(ct);
        var ranks = orderedAirportIds.Take(3).Select((id, i) => new { id, rank = i + 1 }).ToDictionary(x => x.id, x => x.rank);
        foreach (var a in airports)
            a.FeaturedRank = ranks.TryGetValue(a.Id, out var r) ? r : (int?)null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetFeaturedAppsAsync(string accCode, IReadOnlyList<int> orderedAppSectorIds, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var apps = await _db.Sectors.Where(s => s.AccId == fid && s.Type == Vipi.Domain.SectorType.App).ToListAsync(ct);
        var ranks = orderedAppSectorIds.Take(3).Select((id, i) => new { id, rank = i + 1 }).ToDictionary(x => x.id, x => x.rank);
        foreach (var s in apps)
            s.FeaturedRank = ranks.TryGetValue(s.Id, out var r) ? r : (int?)null;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetFeaturedVloasAsync(string accCode, IReadOnlyList<int> orderedVloaDocIds, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var vloas = await _db.Documents
            .Where(d => d.Type == DocumentType.Vloa
                        && d.Parties.Any(pa => pa.Role == PartyRole.Home && pa.Sector!.AccId == fid))
            .ToListAsync(ct);
        var ranks = orderedVloaDocIds.Take(3).Select((id, i) => new { id, rank = i + 1 }).ToDictionary(x => x.id, x => x.rank);
        foreach (var d in vloas)
            d.FeaturedRank = ranks.TryGetValue(d.Id, out var r) ? r : (int?)null;
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> AirportIcaoExistsAsync(string icao, CancellationToken ct = default) =>
        _db.Airports.AnyAsync(a => a.Icao == icao, ct);

    public async Task<int> CreateAirportAsync(string accCode, string icao, string name, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var airport = new Airport { AccId = fid, Icao = icao, Name = name };
        _db.Airports.Add(airport);
        await _db.SaveChangesAsync(ct);
        return airport.Id;
    }

    public async Task DeleteAirportAsync(string accCode, int airportId, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var airport = await _db.Airports.FirstOrDefaultAsync(a => a.Id == airportId && a.AccId == fid, ct);
        if (airport is null) return;
        if (await _db.Sectors.AnyAsync(s => s.AirportId == airportId, ct))
            throw new InvalidOperationException(Lingua("Impossibile eliminare l'aeroporto: dei settori vi puntano.", "The airport cannot be deleted: some sectors point to it."));
        AuditScribe.Write(_db, Attore, AuditAction.Delete, "Airport", airport.Id.ToString(),
            new { airport.Icao, airport.Name, Acc = accCode });
        _db.Airports.Remove(airport);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sposta un aeroporto in un altro ACC — anagrafica, <b>catalogo</b> delle posizioni e proiezione.
    ///
    /// <para>⚠️ <b>Il catalogo è la parte che conta</b>, ed è quella che mancava. I <c>Sector</c> sono una
    /// proiezione: la fonte è <c>AirportSector</c>. Scrivendo solo l'anagrafica e la proiezione, la prima
    /// riproiezione — il giro notturno, o un qualunque salvataggio che la scatena — <b>riportava indietro</b>
    /// i settori, e con loro il padre. Misurato il 5 settembre 2026 su LIBD spostato da LIBB a LIRR:
    /// aeroporto su LIRR, righe di catalogo su LIBB, e alla riproiezione settori di nuovo su LIBB appesi al
    /// CTR di Brindisi.</para>
    ///
    /// <para>Il padre che resta fuori dal nuovo ACC si stacca — in catalogo, non solo nella proiezione, o
    /// tornerebbe anche lui. Un padre cross-ACC resta possibile, ma dev'essere una scelta di chi edita: un
    /// APP che continua a pendere dal CTR del centro che l'aeroporto ha appena lasciato non è una scelta, è
    /// un residuo.</para>
    /// </summary>
    public async Task MoveAirportAsync(int airportId, string targetAccCode, CancellationToken ct = default)
    {
        var targetFid = await AccIdAsync(targetAccCode, ct) ?? throw new InvalidOperationException($"ACC {targetAccCode} inesistente.");
        var airport = await _db.Airports.FirstOrDefaultAsync(a => a.Id == airportId, ct)
            ?? throw new InvalidOperationException(Lingua("Aeroporto inesistente.", "The airport does not exist."));
        if (airport.AccId == targetFid) return;

        // Il codice come lo scrive l'anagrafica: le righe di catalogo lo portano come stringa, e un «lirr»
        // scritto a mano nell'indirizzo si riconoscerebbe solo per caso.
        var targetCode = (await _db.Accs.AsNoTracking().Where(a => a.Id == targetFid).Select(a => a.Code).FirstAsync(ct));

        airport.AccId = targetFid;

        // 1. Il CATALOGO delle posizioni: è la fonte, e senza questo passo tutto il resto è provvisorio.
        var righe = await _db.AirportSectors.Where(x => x.AirportIcao == airport.Icao).ToListAsync(ct);
        foreach (var r in righe) r.AccCode = targetCode;

        // 2. I padri. Sta dentro il nuovo ACC chi è una posizione di questo aeroporto, un settore del centro
        //    di destinazione, o una posizione di un altro suo aeroporto.
        var dentro = righe.Select(r => r.ComposePosition).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var cs in await _db.AccSectors.AsNoTracking().Where(x => x.CenterId == targetCode)
                     .Select(x => x.ComposePosition).ToListAsync(ct))
            dentro.Add(cs);
        foreach (var cs in await _db.AirportSectors.AsNoTracking()
                     .Where(x => x.AccCode == targetCode && x.AirportIcao != airport.Icao)
                     .Select(x => x.ComposePosition).ToListAsync(ct))
            dentro.Add(cs);

        foreach (var r in righe)
            if (r.ParentCallsign is { } p && !dentro.Contains(p)) r.ParentCallsign = null;
        if (airport.ParentCallsign is { } ap && !dentro.Contains(ap)) airport.ParentCallsign = null;

        // 3. La proiezione, subito: chi guarda l'elenco dopo lo spostamento non deve vedere il vecchio ACC
        //    finché non passa il giro notturno. La riproiezione vera la rifarà dai cataloghi, che ora dicono
        //    la stessa cosa.
        var sectors = await _db.Sectors.Where(s => s.AirportId == airportId).ToListAsync(ct);
        var movedIds = sectors.Select(s => s.Id).ToHashSet();
        foreach (var s in sectors)
        {
            s.AccId = targetFid;
            if (s.ParentSectorId is int pid && !movedIds.Contains(pid)) s.ParentSectorId = null;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AirportAdminRow>> ListAllAirportsAsync(CancellationToken ct = default) =>
        await _db.Airports.AsNoTracking()
            .OrderBy(a => a.Acc!.Code).ThenBy(a => a.Icao)
            .Select(a => new AirportAdminRow(a.Id, a.Icao, a.Name, a.Acc!.Code, a.Sectors.Count,
                a.Sectors.Any(s => s.Type == SectorType.Twr || s.Type == SectorType.ITwr), a.IsHidden,
                // Il documento lo dice l'AEROPORTO. Passando dai settori — com'era — serviva escludere a mano
                // l'APP non remotizzato, e uno scalo senza torre non lo trovava mai: la pagina offriva «crea»
                // per un documento che esisteva già.
                a.DocumentId,
                a.HasMilitaryPresence, a.IsMilitaryOnly, a.MilDocumentId))
            .ToListAsync(ct);

    public async Task SetAirportMilitaryOnlyAsync(string accCode, int airportId, bool militaryOnly, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var airport = await _db.Airports.FirstOrDefaultAsync(a => a.Id == airportId && a.AccId == fid, ct)
            ?? throw new InvalidOperationException(Lingua("Aeroporto inesistente nella ACC indicata.", "The airport does not exist in that ACC."));
        // Senza presenza militare la distinzione non ha senso: «solo militare» ne è un sottoinsieme, non un flag
        // indipendente. Tacere e uscire lascerebbe la spunta accesa a schermo e spenta in archivio.
        if (militaryOnly && !airport.HasMilitaryPresence)
            throw new InvalidOperationException(Lingua(
                $"{airport.Icao} non ha presenza militare secondo la sorgente: non può essere «solo militare».",
                $"{airport.Icao} has no military presence according to the source: it cannot be «military only»."));
        if (airport.IsMilitaryOnly == militaryOnly) return;
        airport.IsMilitaryOnly = militaryOnly;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetAirportHiddenAsync(string accCode, int airportId, bool hidden, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var airport = await _db.Airports.FirstOrDefaultAsync(a => a.Id == airportId && a.AccId == fid, ct)
            ?? throw new InvalidOperationException(Lingua("Aeroporto inesistente nella ACC indicata.", "The airport does not exist in that ACC."));
        if (airport.IsHidden == hidden) return;
        airport.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SectorBriefRow>> ListAllSectorsAsync(CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .OrderBy(s => s.Callsign)
            .Select(s => new SectorBriefRow(s.Id, s.Callsign, s.Acc!.Code))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<GlobalSectorRow>> ListSectorNodesAsync(CancellationToken ct = default) =>
        await _db.Sectors.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Callsign)
            .Select(s => new GlobalSectorRow(s.Id, s.Callsign, s.Acc!.Code, s.Acc!.CountryPrefix,
                s.Type, s.Kind, s.ApproachKind, s.ParentSectorId, s.DocumentId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> AutoAssignAirportsAsync(
        IReadOnlyList<(string AccCode, string Icao, string Name)> candidates, CancellationToken ct = default)
    {
        if (candidates.Count == 0) return Array.Empty<string>();

        var accByCode = await _db.Accs.AsNoTracking()
            .ToDictionaryAsync(f => f.Code, f => f.Id, StringComparer.OrdinalIgnoreCase, ct);
        var takenIcaos = (await _db.Airports.AsNoTracking().Select(a => a.Icao).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<string>();
        foreach (var c in candidates)
        {
            var icao = c.Icao.Trim().ToUpperInvariant();
            if (icao.Length != 4) continue;
            if (takenIcaos.Contains(icao)) continue;
            if (!accByCode.TryGetValue(c.AccCode.Trim(), out var fid)) continue;

            _db.Airports.Add(new Airport { AccId = fid, Icao = icao, Name = c.Name.Trim() });
            takenIcaos.Add(icao);   // evita duplicati se la sorgente ha doppioni
            created.Add(icao);
        }

        if (created.Count > 0) await _db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<IReadOnlyList<AirportAccDivergence>> ListAccDivergencesAsync(
        IReadOnlyList<SourceAirport> source, CancellationToken ct = default)
    {
        if (source.Count == 0) return Array.Empty<AirportAccDivergence>();

        var nostri = await _db.Airports.AsNoTracking()
            .Select(a => new { a.Icao, a.Name, Acc = a.Acc!.Code })
            .ToListAsync(ct);

        // Il confronto NON si riscrive qui: la stessa domanda la fa la pagina di gestione aeroporti.
        return AirportAccDivergences.Trova(nostri.Select(a => (a.Icao, a.Name, a.Acc)), source);
    }

    public async Task<int> SyncAirportSourceFieldsAsync(IReadOnlyList<SourceAirport> source, CancellationToken ct = default)
    {
        if (source.Count == 0) return 0;

        // Ultimo vince sui doppioni della sorgente, come fa gia' l'assegnazione: un ICAO ripetuto e' un difetto
        // della sorgente, non un motivo per far fallire il giro.
        var byIcao = new Dictionary<string, SourceAirport>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in source) byIcao[a.Icao.Trim()] = a;

        var airports = await _db.Airports.ToListAsync(ct);
        var changed = 0;
        var visto = DateTime.UtcNow;
        var timbrati = 0;
        foreach (var apt in airports)
        {
            if (!byIcao.TryGetValue(apt.Icao, out var src)) continue;   // fuori dal paese configurato: non lo sappiamo

            // Il timbro va messo PRIMA del confronto e su ogni ICAO nominato, anche quando nessun campo
            // cambia: «la sorgente lo manda ancora» è un fatto per se', ed e' quello che autorizza — o
            // vieta — l'eliminazione. Non conta fra i `changed`, che dicono quanti campi anagrafici sono
            // stati riallineati e finiscono in un messaggio a schermo.
            apt.LastSeenAtUtc = visto;
            timbrati++;

            var before = (apt.HasMilitaryPresence, apt.IsMilitaryOnly, apt.Iata, apt.ElevationFt, apt.MagneticVariation);
            apt.HasMilitaryPresence = src.HasMilitaryPresence;
            // Coerenza: «solo militare» non puo' sopravvivere alla presenza militare che l'ha reso possibile.
            if (!apt.HasMilitaryPresence) apt.IsMilitaryOnly = false;
            apt.Iata = src.Iata;
            apt.ElevationFt = src.ElevationFt;
            apt.MagneticVariation = src.MagneticVariation;
            if (before != (apt.HasMilitaryPresence, apt.IsMilitaryOnly, apt.Iata, apt.ElevationFt, apt.MagneticVariation))
                changed++;
        }

        if (changed > 0 || timbrati > 0) await _db.SaveChangesAsync(ct);
        return changed;
    }

    public async Task<(int Created, bool AirportFound)> EnsureAirportSectorsAsync(
        string icao,
        IReadOnlyList<(SectorType Type, string Callsign, string? Frequency)> positions,
        CancellationToken ct = default)
    {
        var airport = await _db.Airports.FirstOrDefaultAsync(a => a.Icao == icao, ct);
        if (airport is null) return (0, false);

        var accId = airport.AccId;
        var accSectors = await _db.Sectors.Where(s => s.AccId == accId).ToListAsync(ct);
        var byCallsign = accSectors.ToDictionary(s => s.Callsign, s => s, StringComparer.OrdinalIgnoreCase);
        var posByType = positions.GroupBy(p => p.Type).ToDictionary(g => g.Key, g => g.First());
        var created = 0;

        Sector? GetOrCreate(SectorType type, int coverage, string label, ApproachKind? approachKind = null)
        {
            if (!posByType.TryGetValue(type, out var p)) return null;
            if (byCallsign.TryGetValue(p.Callsign, out var ex)) return ex;
            var s = new Sector
            {
                AccId = accId, Callsign = p.Callsign, Type = type, Kind = SectorKind.Airport,
                Name = $"{icao} {label}", DefaultFrequency = p.Frequency, CoverageOrder = coverage,
                AirportId = airport.Id, AirportIcao = icao, ApproachKind = approachKind,
                ImportedAtUtc = DateTime.UtcNow, IsActive = true,
            };
            _db.Sectors.Add(s);
            byCallsign[p.Callsign] = s;
            created++;
            return s;
        }

        // APP d'aeroporto trattato come remotizzato (la doc vive nella vIPI di ACC).
        var app = GetOrCreate(SectorType.App, 5, "Approach", ApproachKind.Remotized);
        var twr = GetOrCreate(SectorType.Twr, 10, "Tower");
        var gnd = GetOrCreate(SectorType.Gnd, 20, "Ground");
        var del = GetOrCreate(SectorType.Del, 30, "Clearance Delivery");
        // Contenimento top-down APP→TWR→GND→DEL (solo per i settori appena creati e senza padre).
        if (twr is not null && app is not null && twr.ParentSectorId is null) twr.ParentSector = app;
        if (gnd is not null && (twr ?? app) is Sector gndParent && gnd.ParentSectorId is null) gnd.ParentSector = gndParent;
        if (del is not null && (gnd ?? twr ?? app) is Sector delParent && del.ParentSectorId is null) del.ParentSector = delParent;

        // Fallback: nessun settore d'aeroporto (né creato né preesistente) → crea almeno il TWR.
        var hasAirportSector = app is not null || twr is not null || gnd is not null || del is not null
            || accSectors.Any(s => s.AirportId == airport.Id);
        if (!hasAirportSector)
        {
            _db.Sectors.Add(new Sector
            {
                AccId = accId, Callsign = $"{icao}_TWR", Type = SectorType.Twr, Kind = SectorKind.Airport,
                Name = $"{icao} Tower", CoverageOrder = 10, AirportId = airport.Id, AirportIcao = icao,
                ImportedAtUtc = DateTime.UtcNow, IsActive = true,
            });
            created++;
        }

        await _db.SaveChangesAsync(ct);
        return (created, true);
    }

    public Task<bool> CallsignExistsAsync(string callsign, CancellationToken ct = default) =>
        _db.Sectors.AnyAsync(s => s.Callsign == callsign, ct);

    public async Task<int> AddSectorAsync(string accCode, string callsign, SectorType type, SectorKind kind, string name,
        string? defaultFrequency, int coverageOrder, ApproachKind? approachKind, int? parentSectorId,
        int? airportId, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        if (parentSectorId is int pid && !await _db.Sectors.AnyAsync(s => s.Id == pid && s.AccId == fid, ct))
            throw new InvalidOperationException(Lingua("Il settore padre non appartiene alla ACC.", "The parent sector does not belong to the ACC."));

        string? icao = null;
        if (kind == SectorKind.Airport && airportId is int aid)
        {
            var airport = await _db.Airports.FirstOrDefaultAsync(a => a.Id == aid && a.AccId == fid, ct)
                ?? throw new InvalidOperationException(Lingua("L'aeroporto non appartiene alla ACC.", "The airport does not belong to the ACC."));
            icao = airport.Icao;
        }
        else airportId = null;

        var sector = new Sector
        {
            AccId = fid, Callsign = callsign, Type = type, Kind = kind, Name = name,
            DefaultFrequency = defaultFrequency, CoverageOrder = coverageOrder, ApproachKind = approachKind,
            ParentSectorId = parentSectorId, AirportId = airportId, AirportIcao = icao,
            ImportedAtUtc = DateTime.UtcNow, IsActive = true,
        };
        _db.Sectors.Add(sector);
        await _db.SaveChangesAsync(ct);
        return sector.Id;
    }

    public async Task DeleteSectorAsync(string accCode, int sectorId, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var sector = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == sectorId && s.AccId == fid, ct);
        if (sector is null) return;
        if (await _db.Sectors.AnyAsync(s => s.ParentSectorId == sectorId, ct))
            throw new InvalidOperationException(Lingua("Impossibile eliminare il settore: ha dei sotto-settori.", "The sector cannot be deleted: it has sub-sectors."));
        // Invariante: un aeroporto non resta senza torre. Blocca la rimozione dell'unica TWR/I_TWR
        // (per rimuoverla, eliminare prima l'intero aeroporto o aggiungere un'altra torre).
        if (sector.AirportId is int aid && (sector.Type is SectorType.Twr or SectorType.ITwr)
            && !await _db.Sectors.AnyAsync(s => s.AirportId == aid && s.Id != sectorId
                && (s.Type == SectorType.Twr || s.Type == SectorType.ITwr), ct))
            throw new InvalidOperationException(Lingua("Impossibile eliminare l'unica torre (TWR/I_TWR) dell'aeroporto: ogni aeroporto deve mantenerne almeno una.", "The airport's only tower (TWR/I_TWR) cannot be deleted: every airport has to keep at least one."));
        AuditScribe.Write(_db, Attore, AuditAction.Delete, "Sector", sector.Id.ToString(),
            new { sector.Callsign, sector.Name, sector.Type, sector.Kind, Acc = accCode });
        _db.Sectors.Remove(sector);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSectorFrequencyAsync(string accCode, int sectorId, string? frequencyMhz, CancellationToken ct = default)
    {
        var fid = await AccIdAsync(accCode, ct) ?? throw new InvalidOperationException($"ACC {accCode} inesistente.");
        var sector = await _db.Sectors.FirstOrDefaultAsync(s => s.Id == sectorId && s.AccId == fid, ct)
                     ?? throw new InvalidOperationException(Lingua("Settore inesistente nella ACC.", "The sector does not exist in the ACC."));
        // Un settore PROIETTATO ha la frequenza come attributo di sorgente: SyncFromCatalogsAsync la riscrive a
        // ogni import/hide/edit (EfSectorProjectionService: DefaultFrequency = catalogo). Editarla qui darebbe
        // l'illusione di una modifica che il prossimo sync cancella in silenzio → si rifiuta. Catalogo = fonte unica.
        if (sector.IsProjected)
            throw new Vipi.Application.Aor.ValidationException(Lingua(
                "La frequenza di un settore proiettato è gestita dalla sorgente (sola lettura): modificala nel catalogo, non qui.",
                "The frequency of a projected sector comes from the source (read-only): change it in the catalogue, not here."));
        var f = (frequencyMhz ?? "").Trim();
        sector.DefaultFrequency = f.Length == 0 ? null : f;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<int?> AccIdAsync(string accCode, CancellationToken ct) =>
        await _db.Accs.Where(f => f.Code == accCode).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);
}
