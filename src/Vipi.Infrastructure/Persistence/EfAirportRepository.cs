using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Persistenza EF del profilo strutturato dell'aeroporto + rigenerazione in-place del documento dalle entità.
/// Le scritture per-area sostituiscono l'intera lista (l'editor invia tutto); il merge da IVAO è invece mirato.
/// </summary>
public sealed class EfAirportRepository : IAirportRepository
{
    private readonly VipiDbContext _db;
    private readonly Vipi.Application.Media.IMediaMaintenance _media;

    public EfAirportRepository(VipiDbContext db, Vipi.Application.Media.IMediaMaintenance media)
    {
        _db = db;
        _media = media;
    }

    public async Task<string?> GetAccCodeByIcaoAsync(string icao, CancellationToken ct = default) =>
        await _db.Airports.Where(a => a.Icao == icao).Select(a => a.Acc!.Code).FirstOrDefaultAsync(ct);

    public async Task<AirportData?> LoadAsync(string icao, CancellationToken ct = default)
    {
        var airport = await _db.Airports.AsNoTracking().Include(a => a.Acc)
            .FirstOrDefaultAsync(a => a.Icao == icao, ct);
        if (airport is null) return null;

        var tls = await _db.AirportTransitionLevels.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order).Select(x => new TlRow(x.Id, x.QnhFrom, x.QnhTo, x.Level)).ToListAsync(ct);
        var rwys = await _db.AirportRunways.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order)
            .Select(x => new RunwayRow(x.Id, x.Ident, x.LengthM, x.Bearing, x.ToraM, x.LdaM, x.AppProcedures, x.Patterns,
                x.Circling, x.ThresholdLat, x.ThresholdLon, x.ThresholdElevationFt, x.NeverDeparture, x.NeverArrival))
            .ToListAsync(ct);
        var rules = await _db.AirportRunwayRules.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order)
            .Select(x => new RunwayRuleRow(x.Id, x.DepRunways, x.ArrRunways, x.Name,
                x.MaxTailwindKt, x.MaxCrosswindKt, x.Surface, x.Note,
                x.TimeFromLocalMin, x.TimeToLocalMin, x.DaysOfWeekMask, x.DateParity,
                x.DateFromMonthDay, x.DateToMonthDay))
            .ToListAsync(ct);
        // ⚠️ Le due famiglie si leggono SEPARATE e per verso: stessa tabella, stessa forma di riga, ma chi
        // guarda le partenze non deve vedere gli arrivi. Una lettura sola da spacchettare dopo costerebbe la
        // stessa query e darebbe a ogni chiamante l'occasione di scordarsi il filtro.
        var procedure = await _db.AirportProcedures.AsNoTracking()
            .Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order)
            .Select(x => new { x.Kind, Riga = new SidRow(x.Id, x.Runway, x.Fix, x.Name, x.Transition, x.InitialClimb, x.Type, x.Cat, x.Wtc, x.Condition,
                x.IsImported, x.Priority, x.StableKey, x.SourceAiracCycle, x.ForcePublished, x.NeedsFixReview, x.InitialClimbByApp,
                x.IsHidden, x.FixOverride, x.TransitionOverride, x.SupersededFromCycle) })
            .ToListAsync(ct);
        var sids = procedure.Where(x => x.Kind == ProcedureKind.Sid).Select(x => x.Riga).ToList();
        var stars = procedure.Where(x => x.Kind == ProcedureKind.Star).Select(x => x.Riga).ToList();

        // I minimi LVP: zero o una riga. L'assenza e' un fatto — «nessuno li ha dichiarati» — e non si
        // sostituisce con dei valori di comodo.
        var lvp = await _db.AirportLvpMinima.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .Select(x => new LvpRow(x.Id, x.Declared, x.PrepRvrM, x.PrepCeilingFt,
                x.LvpRvrM, x.LvpCeilingFt, x.CancelRvrM, x.CancelCeilingFt, x.Note))
            .FirstOrDefaultAsync(ct);

        // Link (riferimento vivo): valore risolto ora dal Sector sorgente (DefaultFrequency).
        var linkRaw = await _db.AirportFrequencyLinks.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order).Include(x => x.SourceSector)
            .Where(x => x.SourceSector != null && x.SourceSector!.DefaultFrequency != null)
            .Select(x => new { x.Id, x.SourceSectorId, x.LabelOverride, x.SourceSector!.Callsign, Freq = x.SourceSector!.DefaultFrequency! })
            .ToListAsync(ct);
        // Etichetta = override staff, altrimenti atcCallsign IVAO (dal catalogo), altrimenti il callsign.
        var atc = await EfAccDerivationRepository.BuildAtcNameMapAsync(_db, ct);
        var links = linkRaw.Select(x => new FrequencyLinkRow(x.Id, x.SourceSectorId,
            x.LabelOverride ?? (atc.TryGetValue(x.Callsign, out var n) ? n : x.Callsign), x.Callsign, x.Freq)).ToList();


        return new AirportData
        {
            AirportId = airport.Id, Icao = airport.Icao, Name = airport.Name, AccCode = airport.Acc!.Code,
            TransitionAltitudeFt = airport.TransitionAltitudeFt,
            MetarStationIcao = airport.MetarStationIcao,
            TransitionLevels = tls, Runways = rwys, Rules = rules, Sids = sids, Stars = stars, Links = links, Lvp = lvp,
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>Due query in tutto</b>, qualunque sia il numero di aeroporti: una per le piste e una per le
    /// regole, filtrate sull'insieme degli id. Il metodo che questa sostituisce ne faceva otto per
    /// aeroporto, in fila.
    ///
    /// <para>⚠️ Il filtro parte dagli ICAO e passa per gli <b>id</b>, non per una join sull'ICAO: le due
    /// tabelle delle piste sono legate all'aeroporto per id (<c>AirportId</c>), e cercarle per ICAO
    /// vorrebbe dire aggiungere una join a ogni riga per un dato che si è già letto.</para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, PisteDiAeroporto>> ListRunwayDataAsync(
        IReadOnlyCollection<string> icaos, CancellationToken ct = default)
    {
        var vuoto = (IReadOnlyDictionary<string, PisteDiAeroporto>)
            new Dictionary<string, PisteDiAeroporto>(StringComparer.OrdinalIgnoreCase);
        if (icaos.Count == 0) return vuoto;

        var cercati = icaos.Select(i => (i ?? "").Trim().ToUpperInvariant()).Where(i => i.Length > 0).ToList();
        if (cercati.Count == 0) return vuoto;

        var idPerIcao = await _db.Airports.AsNoTracking()
            .Where(a => cercati.Contains(a.Icao))
            .Select(a => new { a.Id, a.Icao })
            .ToDictionaryAsync(a => a.Id, a => a.Icao, ct);
        if (idPerIcao.Count == 0) return vuoto;

        var id = idPerIcao.Keys.ToList();

        var piste = (await _db.AirportRunways.AsNoTracking()
                .Where(x => id.Contains(x.AirportId))
                .OrderBy(x => x.AirportId).ThenBy(x => x.Order)
                .Select(x => new { x.AirportId, Riga = new RunwayRow(x.Id, x.Ident, x.LengthM, x.Bearing, x.ToraM, x.LdaM, x.AppProcedures, x.Patterns,
                    x.Circling, x.ThresholdLat, x.ThresholdLon, x.ThresholdElevationFt, x.NeverDeparture, x.NeverArrival) })
                .ToListAsync(ct))
            .GroupBy(x => x.AirportId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RunwayRow>)g.Select(x => x.Riga).ToList());

        var regole = (await _db.AirportRunwayRules.AsNoTracking()
                .Where(x => id.Contains(x.AirportId))
                .OrderBy(x => x.AirportId).ThenBy(x => x.Order)
                .Select(x => new { x.AirportId, Riga = new RunwayRuleRow(x.Id, x.DepRunways, x.ArrRunways, x.Name,
                    x.MaxTailwindKt, x.MaxCrosswindKt, x.Surface, x.Note,
                    x.TimeFromLocalMin, x.TimeToLocalMin, x.DaysOfWeekMask, x.DateParity,
                    x.DateFromMonthDay, x.DateToMonthDay) })
                .ToListAsync(ct))
            .GroupBy(x => x.AirportId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RunwayRuleRow>)g.Select(x => x.Riga).ToList());

        var esito = new Dictionary<string, PisteDiAeroporto>(StringComparer.OrdinalIgnoreCase);
        foreach (var (idAeroporto, icao) in idPerIcao)
            esito[icao] = new PisteDiAeroporto(
                piste.GetValueOrDefault(idAeroporto) ?? Array.Empty<RunwayRow>(),
                regole.GetValueOrDefault(idAeroporto) ?? Array.Empty<RunwayRuleRow>());
        return esito;
    }

    public async Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default)
    {
        var raw = await _db.Sectors.AsNoTracking()
            .Where(s => s.DefaultFrequency != null)
            .OrderBy(s => s.AirportIcao).ThenBy(s => s.Callsign)
            .Select(s => new { s.Id, s.AirportIcao, s.Callsign, Freq = s.DefaultFrequency! })
            .ToListAsync(ct);
        var atc = await EfAccDerivationRepository.BuildAtcNameMapAsync(_db, ct);
        return raw.Select(s => new LinkableFrequencyRow(s.Id, s.AirportIcao, s.Callsign, s.Freq,
            atc.TryGetValue(s.Callsign, out var n) ? n : null)).ToList();
    }

    public async Task<IReadOnlyList<EnteRow>> ListSectorCallsignsAsync(CancellationToken ct = default)
    {
        // ⚠️ Nessun filtro sulla frequenza, ed è tutta la differenza con ListLinkableFrequenciesAsync: il
        // nominativo di un ente non dipende dall'avergliene dichiarata una.
        var raw = await _db.Sectors.AsNoTracking()
            .OrderBy(s => s.AirportIcao).ThenBy(s => s.Callsign)
            .Select(s => new { s.Id, s.AirportIcao, s.Callsign })
            .ToListAsync(ct);
        var atc = await EfAccDerivationRepository.BuildAtcNameMapAsync(_db, ct);
        return raw.Select(s => new EnteRow(s.Id, s.AirportIcao, s.Callsign,
            atc.TryGetValue(s.Callsign, out var n) ? n : null)).ToList();
    }

    public async Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default)
    {
        var a = await _db.Airports.Include(x => x.TransitionLevels)
            .FirstOrDefaultAsync(x => x.Icao == icao, ct) ?? throw NotFound(icao);
        a.TransitionAltitudeFt = ta;
        RecomputeDefaultBandLevels(a);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetMetarStationAsync(string icao, string? station, CancellationToken ct = default)
    {
        var a = await _db.Airports.FirstOrDefaultAsync(x => x.Icao == icao, ct) ?? throw NotFound(icao);
        a.MetarStationIcao = station;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveTransitionLevelsAsync(string icao, IReadOnlyList<TlRow> rows, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        _db.AirportTransitionLevels.RemoveRange(_db.AirportTransitionLevels.Where(x => x.AirportId == id));
        for (var i = 0; i < rows.Count; i++)
            _db.AirportTransitionLevels.Add(new AirportTransitionLevel
            {
                AirportId = id, Order = i, QnhFrom = rows[i].QnhFrom, QnhTo = rows[i].QnhTo, Level = rows[i].Level.Trim(),
            });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Le coordinate della soglia e la sua elevazione, dalla sorgente. ⚠️ <b>L'assenza non cancella</b>: un
    /// giro che non le porta lascia quelle che ci sono. È la stessa regola dell'anagrafica radioassistenze,
    /// e la stessa che azzerò 83 poligoni su 83 quando non c'era.
    /// </summary>
    private static void Soglia(AirportRunway riga, SourceRunway rw)
    {
        if (rw.ThresholdLat is { } la && rw.ThresholdLon is { } lo)
        {
            riga.ThresholdLat = la;
            riga.ThresholdLon = lo;
        }
        if (rw.ElevationFt is { } e) riga.ThresholdElevationFt = e;
    }

    public async Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        var vecchie = await _db.AirportRunways.AsNoTracking().Where(x => x.AirportId == id).ToListAsync(ct);

        // ⚠️ I campi di SORGENTE si riportano per IDENT. Questo salvataggio cancella e riscrive le righe — è
        // l'unico modo di gestire ordine e cancellazioni in un colpo — e le coordinate della soglia non
        // passano dall'editor: senza questa riga sparirebbero al primo salvataggio di una colonna qualsiasi,
        // e sarebbero tornate solo al re-import successivo. Nessun errore, nessun avviso: una tabella che si
        // svuota da sola.
        var perIdent = vecchie.ToDictionary(x => x.Ident.Trim().ToUpperInvariant(), x => x,
            StringComparer.OrdinalIgnoreCase);

        _db.AirportRunways.RemoveRange(_db.AirportRunways.Where(x => x.AirportId == id));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var ident = r.Ident.Trim().ToUpperInvariant();
            perIdent.TryGetValue(ident, out var prima);
            _db.AirportRunways.Add(new AirportRunway
            {
                AirportId = id, Order = i, Ident = ident, LengthM = r.LengthM, Bearing = r.Bearing,
                ToraM = r.ToraM, LdaM = r.LdaM, AppProcedures = r.AppProcedures, Patterns = r.Patterns, Circling = r.Circling,
                NeverDeparture = r.NeverDeparture, NeverArrival = r.NeverArrival,   // editoriali: dall'editor, con la riga
                ThresholdLat = prima?.ThresholdLat, ThresholdLon = prima?.ThresholdLon,
                ThresholdElevationFt = prima?.ThresholdElevationFt,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveRunwayRulesAsync(string icao, IReadOnlyList<RunwayRuleRow> rows, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        _db.AirportRunwayRules.RemoveRange(_db.AirportRunwayRules.Where(x => x.AirportId == id));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            _db.AirportRunwayRules.Add(new AirportRunwayRule
            {
                AirportId = id, Order = i, Name = string.IsNullOrWhiteSpace(r.Name) ? null : r.Name!.Trim(),
                DepRunways = (r.DepRunways ?? "").Trim(), ArrRunways = (r.ArrRunways ?? "").Trim(),
                MaxTailwindKt = r.MaxTailwindKt, MaxCrosswindKt = r.MaxCrosswindKt, Surface = r.Surface, Note = r.Note,
                TimeFromLocalMin = r.TimeFromLocalMin, TimeToLocalMin = r.TimeToLocalMin,
                DaysOfWeekMask = r.DaysOfWeekMask, DateParity = r.DateParity,
                DateFromMonthDay = r.DateFromMonthDay, DateToMonthDay = r.DateToMonthDay,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveLvpAsync(string icao, LvpRow? row, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        // Cancella e riscrivi, come le altre tabelle del profilo: la riga e' al massimo una, e cosi' «togliere
        // i minimi» e «cambiarli» passano dalla stessa porta.
        _db.AirportLvpMinima.RemoveRange(_db.AirportLvpMinima.Where(x => x.AirportId == id));
        if (row is not null)
        {
            _db.AirportLvpMinima.Add(new AirportLvpMinima
            {
                AirportId = id, Declared = row.Declared,
                PrepRvrM = row.PrepRvrM, PrepCeilingFt = row.PrepCeilingFt,
                LvpRvrM = row.LvpRvrM, LvpCeilingFt = row.LvpCeilingFt,
                CancelRvrM = row.CancelRvrM, CancelCeilingFt = row.CancelCeilingFt,
                Note = string.IsNullOrWhiteSpace(row.Note) ? null : row.Note!.Trim(),
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveSidsAsync(string icao, ProcedureKind kind, IReadOnlyList<SidRow> rows, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        // Origin-aware: sostituisce SOLO le righe manuali DI QUEL VERSO; le importate (IsImported=true) e le
        // procedure dell'altro verso restano intatte.
        _db.AirportProcedures.RemoveRange(_db.AirportProcedures
            .Where(x => x.AirportId == id && x.Kind == kind && !x.IsImported));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            _db.AirportProcedures.Add(new AirportProcedure
            {
                AirportId = id, Kind = kind,
                Order = i, Runway = r.Runway, Fix = r.Fix.Trim(), Name = r.Name.Trim(),
                Transition = r.Transition, InitialClimb = r.InitialClimb, InitialClimbByApp = r.InitialClimbByApp,
                Type = r.Type, Cat = r.Cat, Wtc = r.Wtc, Condition = r.Condition,
                // La priorità fra SID dello stesso punto vale anche per le righe a mano: la colonna esisteva
                // già (tabella unica con le importate), ma qui non veniva scritta e si perdeva a ogni salvataggio.
                Priority = r.Priority,
                IsHidden = r.IsHidden,
                IsImported = false,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReplaceImportedProceduresAsync(string icao, ProcedureKind kind,
        IReadOnlyList<ImportedProcedure> rows, string airacCycle, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        // Le importate dell'import precedente, per riapplicare alle righe nuove quel che la sorgente non conosce:
        // priorità e forzatura, il fix risolto a mano, gli arricchimenti, le decisioni dello staff e il PRIMO ciclo
        // d'entrata. Come si abbinano lo dice `Riaggancia`.
        //
        // ⚠️ Anche le versioni «sostituite» (U-003) sono candidate, ma DOPO quelle vive: a parità di nome si
        // continua la riga in vigore, e una sostituita si riprende solo se la sorgente rimanda proprio lei.
        var priorRows = await _db.AirportProcedures.AsNoTracking()
            .Where(x => x.AirportId == id && x.Kind == kind && x.IsImported)
            .OrderBy(x => x.SupersededFromCycle != null).ThenBy(x => x.Id)
            .ToListAsync(ct);
        var abbinate = Riaggancia(icao, kind, priorRows, rows);
        var continuate = new HashSet<int>(abbinate.Where(p => p is not null).Select(p => p!.Id));

        _db.AirportProcedures.RemoveRange(_db.AirportProcedures
            .Where(x => x.AirportId == id && x.Kind == kind && x.IsImported));

        // Le importate dopo le manuali; l'ordine di resa reale è per fix/priorità nel viewer. Gli arrivi partono
        // più in alto delle partenze: ogni lettura filtra comunque per verso, ma una tabella guardata a mano —
        // in diagnostica, in una copia del database — resta leggibile.
        // ⚠️ Lo scaglione degli arrivi sta LARGO: con 1000 e 2000 bastava uno scalo con più di mille partenze
        // perché gli ordini dei due versi si accavallassero. Nessuna lettura ne soffrirebbe — filtrano tutte
        // per verso — ma una tabella guardata a mano, in diagnostica o su una copia, diventerebbe illeggibile
        // proprio quando serve. Misurato: 206 SID a LIRF, il massimo dell'archivio.
        var baseOrder = kind == ProcedureKind.Star ? 1_000_000 : 1_000;
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var p = abbinate[i];
            var found = p is not null;

            // `airacCycle` è il ciclo DAL QUALE la riga vale, deciso da SidStampCycle su quel che la sorgente
            // dichiara (carta §AW2). Se il contenuto è invariato dall'import precedente si conserva il PRIMO:
            // così, raggiunto quel ciclo, la SID diventa pubblica (IsPublicAt) e ci RESTA. Solo un contenuto
            // cambiato — una revisione nuova — riparte dal ciclo d'entrata appena calcolato.
            var invariata = found && ContentUnchanged(p!, r);
            var sourceCycle = invariata ? (p!.SourceAiracCycle ?? airacCycle) : airacCycle;

            // 🔴 U-003: una revisione nuova non cancella la versione in vigore. Resta, sostituita dal ciclo
            // d'entrata della nuova, e ognuna delle due si vede nel suo tratto.
            var vecchiaConservata = found && !invariata && ConservaVersioneVecchia(p!, airacCycle);

            // Se la sorgente ripropone il prefisso grezzo (NeedsFixReview) ma quel fix era già stato risolto a mano,
            // conserva la risoluzione invece di ripristinare il grezzo a ogni reimport.
            var fix = r.Fix.Trim();
            var needsReview = r.NeedsFixReview;
            if (found && r.NeedsFixReview && !p!.NeedsFixReview && !string.IsNullOrWhiteSpace(p.Fix))
            {
                fix = p.Fix!.Trim();
                needsReview = false;
            }

            _db.AirportProcedures.Add(new AirportProcedure
            {
                AirportId = id, Kind = kind,
                Order = baseOrder + i, Runway = r.Runway, Fix = fix, Name = r.Name.Trim(),
                Transition = r.Transition, Type = r.Type,
                IsImported = true, StableKey = r.StableKey, SourceAiracCycle = sourceCycle,
                NeedsFixReview = needsReview,
                // ⚠️ La forzatura NON passa a una revisione nuova quando la vecchia resta come sostituita: era una
                // decisione su quel contenuto — «pubblicalo adesso» — e il buco che copriva non c'è più. Passandola,
                // la nuova usciva insieme alla vecchia: due SID dello stesso punto e della stessa pista (LIRN,
                // ALAX6G e ALAX7G, prova sulla copia del 27 settembre 2026).
                Priority = p?.Priority, ForcePublished = !vecchiaConservata && (p?.ForcePublished ?? false),
                // Arricchimenti editoriali sovrapposti a mano: sopravvivono al reimport (la sorgente non li fornisce).
                InitialClimb = p?.InitialClimb, InitialClimbByApp = p?.InitialClimbByApp ?? false,
                Cat = p?.Cat, Wtc = p?.Wtc, Condition = p?.Condition,
                // Decisioni dello staff sulla riga: nasconderla, correggerne punto e transition. Come gli
                // arricchimenti, la sorgente non le conosce e un reimport non deve disfarle.
                IsHidden = p?.IsHidden ?? false, FixOverride = p?.FixOverride, TransitionOverride = p?.TransitionOverride,
            });
        }

        // 🔴 U-003: quel che la sorgente non manda più vale ancora fino al ciclo che dichiara adesso.
        foreach (var p in priorRows.Where(x => !continuate.Contains(x.Id)))
            ConservaVersioneVecchia(p, airacCycle);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Una riga dell'import precedente che non continua così com'è — rivista, o tolta dalla sorgente — si rimette
    /// in archivio come versione <b>sostituita</b>, se serve ancora a qualche ciclo (U-003, revisione totale 3).
    ///
    /// <para>Fino al 27 settembre 2026 si cancellava: la nuova aspettava il suo ciclo d'entrata e in mezzo non c'era
    /// niente — il 25 settembre a LIMF le TOP1B con transizione sono sparite dal vSOP pubblico fino al 1° ottobre.
    /// Ora la vecchia vale fino al ciclo che la sorgente dichiara (<see cref="SidRow.IsPublicAt"/>), con tutte le
    /// sue decisioni.</para>
    /// <list type="bullet">
    ///   <item><b>viva</b>: si tiene se era entrata PRIMA del ciclo dichiarato. Entrata nello stesso ciclo, era una
    ///         correzione dentro il ciclo: la sorgente ha cambiato idea, e si toglie come prima.</item>
    ///   <item><b>già sostituita</b>: si tiene finché il ciclo dichiarato non la supera; dopo non serve più a nessun
    ///         ciclo e si toglie.</item>
    /// </list>
    /// </summary>
    /// <returns>Vero se la versione vecchia è rimasta in archivio.</returns>
    private bool ConservaVersioneVecchia(AirportProcedure p, string cicloDichiarato)
    {
        string? dal;
        if (p.SupersededFromCycle is { } gia) dal = Prima(cicloDichiarato, gia) || cicloDichiarato == gia ? gia : null;
        else dal = Prima(p.SourceAiracCycle, cicloDichiarato) ? cicloDichiarato : null;
        if (dal is null) return false;

        _db.AirportProcedures.Add(new AirportProcedure
        {
            AirportId = p.AirportId, Kind = p.Kind, Order = p.Order, Runway = p.Runway, Fix = p.Fix, Name = p.Name,
            Transition = p.Transition, Type = p.Type, IsImported = true, StableKey = p.StableKey,
            SourceAiracCycle = p.SourceAiracCycle, SupersededFromCycle = dal, NeedsFixReview = p.NeedsFixReview,
            Priority = p.Priority, ForcePublished = p.ForcePublished,
            InitialClimb = p.InitialClimb, InitialClimbByApp = p.InitialClimbByApp, Cat = p.Cat, Wtc = p.Wtc,
            Condition = p.Condition, IsHidden = p.IsHidden, FixOverride = p.FixOverride, TransitionOverride = p.TransitionOverride,
        });
        return true;
    }

    private static readonly Vipi.Domain.Services.AiracService Airac = new();

    /// <summary>Vero se il ciclo <paramref name="a"/> entra in vigore prima di <paramref name="b"/>. Per DATA, non
    /// per stringa («2701» viene dopo «2613»); un ciclo che manca o non si legge non è «prima» di niente.</summary>
    private static bool Prima(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        try { return Airac.EffectiveUtcForCycle(a) < Airac.EffectiveUtcForCycle(b); }
        catch (ArgumentException) { return false; }
    }

    // "Contenuto invariato" = stessi campi che definiscono la SID lato sorgente (codice con revisione, transition, tipo).
    // Prefisso e pista fanno parte della chiave, quindi qui non si riconfrontano.
    private static bool ContentUnchanged(AirportProcedure p, ImportedProcedure r) =>
        string.Equals(p.Name, r.Name.Trim(), StringComparison.Ordinal)
        && string.Equals(p.Transition ?? "", r.Transition ?? "", StringComparison.Ordinal)
        && string.Equals(p.Type ?? "", r.Type ?? "", StringComparison.Ordinal);

    /// <summary>
    /// Per ogni riga nuova, la riga dell'import precedente che ne è la continuazione (o null: è nata adesso).
    ///
    /// <para>🔴 <b>La chiave si RICALCOLA dai dati delle righe, non si legge quella salvata</b> (U-005, revisione
    /// totale 3). Quella salvata fino al 27 settembre 2026 conteneva il punto risolto, e un alias nuovo bastava a
    /// staccare la riga dal suo passato. Ricalcolata con <see cref="Sectorfile.AuroraSectorfileParser.ChiaveStabile"/>
    /// sul nome, la transition e la pista, vale anche per le righe scritte nel formato vecchio: niente migrazione.</para>
    ///
    /// <para>🔴 <b>Una riga vecchia si abbina a UNA riga nuova, in tre passi</b> (U-004). La chiave esclude la cifra
    /// della revisione, e nei file ci sono coppie di procedure diverse che la condividono e convivono (ROBO1H/ROBO5H
    /// a LIBG, XIB5A-OKU5R/OKU6A a LIRF, VOG1K/VOG1S a LIME). Col first-wins di prima la seconda della coppia
    /// ereditava le decisioni della prima e, confrontata col nome della prima, prendeva il ciclo nuovo a ogni giro:
    /// restava fuori dalla pagina pubblica per dieci-dodici giorni a ogni ciclo. Ora:</para>
    /// <list type="number">
    ///   <item><b>stesso nome</b>: è la stessa procedura, invariata;</item>
    ///   <item><b>stessa radice del nome</b> (le cifre non contano: <c>XIB?A-OKU?R</c>): la stessa procedura
    ///         rivista, senza confonderla con la sorella che ha un'altra transition;</item>
    ///   <item><b>stessa chiave</b>: la revisione nuova di una procedura il cui nome è cambiato di più.</item>
    /// </list>
    /// <para>Le righe vecchie si prendono in ordine di Id: a parità, l'esito è lo stesso a ogni giro.</para>
    ///
    /// <para>Prima ancora (luglio 2026) il dizionario a chiave unica lanciava «An item with the same key has already
    /// been added» al primo reimport degli scali con queste coppie: una lista, qui, non ha quel problema.</para>
    /// </summary>
    private static AirportProcedure?[] Riaggancia(string icao, ProcedureKind kind,
        IReadOnlyList<AirportProcedure> vecchie, IReadOnlyList<ImportedProcedure> nuove)
    {
        string Chiave(string nome, string? transition, string? pista) =>
            Sectorfile.AuroraSectorfileParser.ChiaveStabile(kind, icao, nome, transition, pista);

        var v = vecchie.Select(x => (Riga: x, Chiave: Chiave(x.Name, x.Transition, x.Runway),
            Nome: x.Name.Trim().ToUpperInvariant())).ToList();
        var n = nuove.Select(x => (Chiave: Chiave(x.Name, x.Transition, x.Runway),
            Nome: x.Name.Trim().ToUpperInvariant())).ToList();
        var usata = new bool[v.Count];
        var esito = new AirportProcedure?[n.Count];

        var passi = new Func<string, string, bool>[]
        {
            (a, b) => string.Equals(a, b, StringComparison.Ordinal),
            (a, b) => string.Equals(Radice(a), Radice(b), StringComparison.Ordinal),
            (_, _) => true,
        };
        foreach (var stessa in passi)
            for (var i = 0; i < n.Count; i++)
            {
                if (esito[i] is not null) continue;
                for (var j = 0; j < v.Count; j++)
                {
                    if (usata[j] || !string.Equals(v[j].Chiave, n[i].Chiave, StringComparison.Ordinal)) continue;
                    if (!stessa(v[j].Nome, n[i].Nome)) continue;
                    esito[i] = v[j].Riga;
                    usata[j] = true;
                    break;
                }
            }
        return esito;
    }

    /// <summary>Il nome senza le cifre delle revisioni: <c>XIB5A-OKU6A</c> → <c>XIB?A-OKU?A</c>.</summary>
    private static string Radice(string nome) =>
        string.Create(nome.Length, nome, (span, s) =>
        {
            for (var k = 0; k < s.Length; k++) span[k] = char.IsAsciiDigit(s[k]) ? '?' : s[k];
        });

    public async Task<int> SetImportedSidsHiddenAsync(string icao, IReadOnlyCollection<int> sidIds, bool hidden, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        var ids = sidIds.Distinct().ToList();
        // ⚠️ Filtrate per SCALO oltre che per id: il lock garantito dal service è quello di questo ICAO, e un id
        // di un altro aeroporto non deve poter passare di qui.
        var righe = await _db.AirportProcedures
            .Where(x => x.AirportId == id && x.IsImported && ids.Contains(x.Id))
            .ToListAsync(ct);
        foreach (var s in righe) s.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
        return righe.Count;
    }

    public async Task SetImportedSidOverridesAsync(string icao, int sidId, string? fixOverride, string? transitionOverride, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        var s = await _db.AirportProcedures.FirstOrDefaultAsync(x => x.Id == sidId && x.AirportId == id && x.IsImported, ct);
        if (s is null) return;
        // Uguale alla sorgente = nessuna correzione: si torna a seguire la sorgente, anche quando cambierà.
        var fix = Blank(fixOverride)?.ToUpperInvariant();
        s.FixOverride = fix is null || string.Equals(fix, s.Fix, StringComparison.OrdinalIgnoreCase) ? null : fix;
        var trans = Blank(transitionOverride)?.ToUpperInvariant();
        s.TransitionOverride = trans is null || string.Equals(trans, s.Transition, StringComparison.OrdinalIgnoreCase) ? null : trans;
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateImportedSidAsync(int sidId, int? priority, bool forcePublished, string? resolvedFix,
        string? initialClimb, bool initialClimbByApp, string? cat, string? wtc, string? condition, CancellationToken ct = default)
    {
        var s = await _db.AirportProcedures.FirstOrDefaultAsync(x => x.Id == sidId && x.IsImported, ct);
        if (s is null) return;
        s.Priority = priority;
        s.ForcePublished = forcePublished;
        // Arricchimenti editoriali: null/vuoto = campo cancellato (Trim per non salvare spazi).
        s.InitialClimb = Blank(initialClimb);
        s.InitialClimbByApp = initialClimbByApp;
        s.Cat = Blank(cat);
        s.Wtc = Blank(wtc);
        s.Condition = Blank(condition);
        if (!string.IsNullOrWhiteSpace(resolvedFix))
        {
            s.Fix = resolvedFix.Trim();
            s.NeedsFixReview = false;
        }
        await _db.SaveChangesAsync(ct);
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public async Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceSectorIds, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        _db.AirportFrequencyLinks.RemoveRange(_db.AirportFrequencyLinks.Where(x => x.AirportId == id));
        var valid = await _db.Sectors.Where(s => sourceSectorIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        var order = 0;
        foreach (var sid in sourceSectorIds.Where(valid.Contains))
            _db.AirportFrequencyLinks.Add(new AirportFrequencyLink { AirportId = id, Order = order++, SourceSectorId = sid });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<RunwayMergeOutcome> MergeFromSourceAsync(string icao, int? transitionAltitude,
        IReadOnlyList<SourceRunway> runways, CancellationToken ct = default)
    {
        var airport = await _db.Airports.Include(a => a.Runways).Include(a => a.TransitionLevels)
            .FirstOrDefaultAsync(a => a.Icao == icao, ct) ?? throw NotFound(icao);

        if (transitionAltitude is int ta) airport.TransitionAltitudeFt = ta;

        // ⚠️ Lista vuota = «nessun cambio». NON è «l'aeroporto non ha più piste»: è come SourceMergeInputs
        // esprime la categoria esclusa dalla policy, ed è anche quel che resta di una fetch a vuoto, perché
        // l'import piste è best-effort silenzioso (IVAO 4xx → zero piste, nessun errore). Senza questa
        // guardia la riconciliazione qui sotto svuoterebbe la tabella a ogni sorgente muta.
        var esito = runways.Count == 0 ? RunwayMergeOutcome.None : RiconciliaPiste(airport, runways);

        // Tabella Transition Level standard (TL = TA + margine per fascia QNH) se non ancora impostata.
        EnsureDefaultTransitionLevels(airport);
        // Con TA di sorgente (bottone "Salva TA" bloccato) questo è l'unico path che aggiorna la TA: ricalcola
        // qui le righe di fascia-default già esistenti, altrimenti resterebbero sull'ultima TA (o "TA + N ft").
        RecomputeDefaultBandLevels(airport);

        await _db.SaveChangesAsync(ct);
        return esito;
    }

    /// <summary>
    /// Le piste dell'aeroporto riportate a quel che la sorgente dice ADESSO: aggiorna quelle che ci sono,
    /// aggiunge quelle nuove, e affronta le <b>orfane</b> — in archivio ma non più nominate dalla sorgente.
    /// <para>Un'orfana <b>vuota</b> se ne va in silenzio: non c'è niente da perdere. Un'orfana con lavoro
    /// editoriale <b>resta</b> e viene nominata nell'esito: la toglierà una persona, dopo aver spostato
    /// TORA/LDA sulla pista nuova. Il merge non deve poter distruggere lavoro umano senza dirlo.</para>
    /// <para>L'<c>Order</c> si rinumera nell'ordine della sorgente, con le orfane in coda: prima le nuove
    /// venivano accodate e basta, e dopo la ri-denominazione di Rimini l'editor mostrava 13, 31, 12, 30 —
    /// le due morte davanti alle due vive.</para>
    /// </summary>
    private static RunwayMergeOutcome RiconciliaPiste(Airport airport, IReadOnlyList<SourceRunway> runways)
    {
        int aggiunte = 0, aggiornate = 0, ordine = 0;
        var vive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rw in runways)
        {
            var ident = rw.Ident.Trim().ToUpperInvariant();
            // La stessa testata due volte nella risposta: la prima vince, la seconda non genera un doppione.
            if (!vive.Add(ident)) continue;

            var ex = airport.Runways.FirstOrDefault(
                r => string.Equals(r.Ident.Trim(), ident, StringComparison.OrdinalIgnoreCase));
            if (ex is not null)
            {
                ex.Ident = ident;                   // normalizza le righe scritte da codice più vecchio
                ex.Order = ordine++;
                ex.LengthM = rw.LengthM;            // sovrascrive solo i campi IVAO
                ex.Bearing = rw.Bearing ?? BearingFromIdent(ident) ?? ex.Bearing;
                Soglia(ex, rw);
                aggiornate++;
            }
            else
            {
                var nuova = new AirportRunway
                {
                    AirportId = airport.Id, Order = ordine++, Ident = ident,
                    LengthM = rw.LengthM, Bearing = rw.Bearing ?? BearingFromIdent(ident),
                };
                Soglia(nuova, rw);
                airport.Runways.Add(nuova);
                aggiunte++;
            }
        }

        var orfane = airport.Runways.Where(r => !vive.Contains(r.Ident.Trim())).ToList();
        var conLavoro = new List<string>();
        foreach (var o in orfane)
        {
            if (SenzaLavoroEditoriale(o)) airport.Runways.Remove(o);
            else { o.Order = ordine++; conLavoro.Add(o.Ident); }
        }

        return new RunwayMergeOutcome(aggiunte, aggiornate, orfane.Count - conLavoro.Count, conLavoro);
    }

    /// <summary>Vero se sulla pista non c'è nulla che una persona abbia scritto: le cinque colonne editoriali
    /// sono tutte vuote e non è marcata «mai in partenza» né «mai in arrivo». È l'unica condizione che autorizza il merge a togliere una riga.
    /// ⚠️ Il flag conta come lavoro: è una scelta di una persona quanto un TORA scritto a mano.</summary>
    private static bool SenzaLavoroEditoriale(AirportRunway r) =>
        string.IsNullOrWhiteSpace(r.ToraM) && string.IsNullOrWhiteSpace(r.LdaM)
        && string.IsNullOrWhiteSpace(r.AppProcedures) && string.IsNullOrWhiteSpace(r.Patterns)
        && string.IsNullOrWhiteSpace(r.Circling) && !r.NeverDeparture && !r.NeverArrival;

    public async Task<int> EnsureDocumentAsync(string icao, CancellationToken ct = default)
    {
        var airport = await _db.Airports
            .Include(a => a.TransitionLevels).Include(a => a.Runways).Include(a => a.RunwayRules)
            .FirstOrDefaultAsync(a => a.Icao == icao, ct) ?? throw NotFound(icao);

        // Garantisce la tabella TL di default anche per aeroporti generati senza import IVAO (es. TA/TL mai popolate).
        EnsureDefaultTransitionLevels(airport);
        // Risolve i livelli delle fasce-default se la TA è nota ma le righe portano ancora il placeholder "TA + N ft"
        // (seminate quando la TA non era ancora arrivata dalla sorgente): senza questo la pagina mostrerebbe i
        // placeholder invece dei FL calcolati. Le fasce personalizzate restano intatte.
        RecomputeDefaultBandLevels(airport);

        // Solo i settori-FOGLIA dell'aeroporto (DEL/GND/TWR/ITwr) appartengono alla vIPI d'aeroporto.
        // Gli APP NON ci vanno mai: se sono "di ACC" stanno nella vIPI di ACC, se standalone hanno doc proprio.
        // Ordino per (int)Type in MEMORIA: Type è un enum salvato come stringa, quindi ORDER BY (int)Type in SQL
        // genera CAST("Type" AS integer) → su Postgres 'Twr'→integer lancia 22P02 (su SQLite tornava 0 in silenzio).
        var sectors = (await _db.Sectors.Where(s => s.AirportId == airport.Id && s.Type != SectorType.App)
            .ToListAsync(ct))
            .OrderBy(s => (int)s.Type).ToList();

        var now = DateTime.UtcNow;
        var cycle = new AiracService().GetCycle(now);

        // Documento esistente: lo dice l'AEROPORTO. Chiedendolo ai settori — com'era fino al 25 agosto 2026 —
        // uno scalo senza torre non lo ritrovava mai e se ne creava uno nuovo a ogni apertura dell'editor.
        Document doc;
        if (airport.DocumentId is int existing)
        {
            doc = await _db.Documents.FirstAsync(d => d.Id == existing, ct);
        }
        else
        {
            // Alla prima generazione il documento resta in BOZZA: l'aeroporto appena importato non è ancora
            // pubblico. Sarà lo staff a pubblicarlo a mano da /services/vsop/versioni.
            // La nascita è condivisa con le altre tre famiglie (Seed/DocumentBirth). ⚠️ Due cose restano
            // dell'aeroporto e si dichiarano qui, perché sono scelte sue e non del catalogo: le SID nascono
            // LIVE (una SID si mostra sempre aggiornata) e le sezioni NON ricevono blocchi segnaposto — non
            // li hanno mai avuti, e la pagina le disegna per chiave, non perché abbiano un blocco dentro.
            // Su `puntaAllaVersione` c'è una domanda aperta: sta scritta in DocumentBirth.
            (doc, _) = Seed.DocumentBirth.Crea(_db, new AiracService(), $"vIPI — {icao} {airport.Name}",
                Language.It, SectionProfile.Airport, authorUserId: 0,
                nasceLive: Seed.DocumentBirth.NasceLive(SectionProfile.Airport), conSegnaposto: false);
            await _db.SaveChangesAsync(ct);
            // ⚠️ `CurrentVersionId` resta NULL, e adesso e' come nascono tutte e quattro le famiglie.
            // Qui veniva impostato sulla versione appena creata, che e' una BOZZA — ma quel campo vuol dire
            // «la versione PUBBLICATA corrente»: lo scrive `PublishAsync`, e l'eliminazione lo azzera.
            // Un documento mai pubblicato che dichiara di averne una dice una cosa falsa.

            // Il legame che conta: il documento è dell'AEROPORTO. Vale anche per uno scalo senza nemmeno un
            // settore proprio — LIBG ha in IVAO solo un APP non remotizzato, e la sua vIPI d'aeroporto ora esiste.
            airport.DocumentId = doc.Id;
        }

        // Riallineamento dei settori al documento dell'aeroporto: un settore comparso DOPO la prima generazione
        // (una torre che IVAO aggiunge più tardi) resterebbe altrimenti scollegato per sempre, e chi parte dal
        // suo callsign non troverebbe il documento che pure esiste.
        if (sectors.Count > 0)
        {
            var primario = sectors.FirstOrDefault(s => IsTower(s.Type)) ?? sectors[0];
            foreach (var s in sectors) { s.DocumentId = doc.Id; s.IsPrimary = s == primario; }
        }

        // Correzione/idempotenza: sgancia eventuali APP di questo aeroporto erroneamente legati a questa vIPI
        // d'aeroporto (binding storico). Da qui in poi torneranno selezionabili in «Nuovo documento».
        var strayApps = await _db.Sectors
            .Where(s => s.AirportId == airport.Id && s.Type == SectorType.App && s.DocumentId == doc.Id)
            .ToListAsync(ct);
        foreach (var s in strayApps) { s.DocumentId = null; s.IsPrimary = false; }

        await _db.SaveChangesAsync(ct);
        return doc.Id;
    }

    /// <summary>
    /// Semina le sezioni del profilo aeroporto (carta 2026-08-26 §1a) sulla versione appena creata: chiave, titolo e
    /// ordine dal <see cref="SectionCatalog"/>, <b>senza blocchi</b> — il corpo delle sezioni fisse lo produce la
    /// pagina, derivandolo dalle tabelle del profilo.
    /// <para>
    /// ⚠️ Il <see cref="RenderMode"/> di nascita non è uniforme: <c>weather</c> e <c>sids</c> nascono
    /// <see cref="RenderMode.Live"/>, le altre <see cref="RenderMode.Frozen"/> (il default della colonna). Il meteo
    /// perché congelarlo sarebbe una bugia, le SID perché lo erano già (doc 10 §S4c) e il loro ciclo AIRAC è
    /// governato dal gate d'import, non dalla release.
    /// </para>
    /// </summary>
    // La regola «nasce Live» (meteo, SID e STAR) sta in DocumentBirth.NasceLive: la usa anche la manutenzione
    // d'avvio, che aggiunge le sezioni mancanti ai documenti già scritti (revisione 3, U-245).

            public async Task<int?> GetDocumentIdAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        // Dall'AEROPORTO, non dai suoi settori: è il legame autoritativo (vedi Airport.Document). Passando dai
        // settori, uno scalo con il solo APP non remotizzato — LIBG — non trovava mai il proprio documento.
        return await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == icao).Select(a => a.DocumentId).FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<AirportMilitaryState?> GetMilitaryStateAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        // ⚠️ Una proiezione sola: i quattro campi si leggono INSIEME perché insieme si decide. Vedi
        // AirportMilitaryState.
        return await _db.Airports.AsNoTracking()
            .Where(a => a.Icao == icao)
            .Select(a => new AirportMilitaryState(
                a.HasMilitaryPresence, a.Category, a.DocumentId, a.MilDocumentId))
            .FirstOrDefaultAsync(ct);
    }

        // ---- helper ----

    private async Task<int> AirportIdAsync(string icao, CancellationToken ct) =>
        await _db.Airports.Where(a => a.Icao == icao).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw NotFound(icao);

    private static InvalidOperationException NotFound(string icao) => new(Lingua($"Aeroporto {icao} inesistente.", $"Airport {icao} does not exist."));

    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();

    /// <summary>TWR e I_TWR (AFIS) sono entrambe "torri" ai fini di frequenza primaria/etichetta.</summary>
    private static bool IsTower(SectorType type) => type is SectorType.Twr or SectorType.ITwr;

    // Ordine e nome vengono da FrequencyPositions (Application). La copia che stava qui era divergente: usava
    // `position ?? "—"`, quindi una posizione di soli spazi rendeva una cella BIANCA nel documento aeroporto
    // mentre ACC e APP rendevano il trattino. Ora il comportamento è uno solo (nessuna cella vuota).
    private static int FreqOrder(AirportSector s) => FrequencyPositions.OrderOf(s.Position);

    private static string FreqNameForPosition(string? position) => FrequencyPositions.NameOf(position);

    private static int? BearingFromIdent(string ident)
    {
        var digits = new string(ident.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? (n * 10) % 360 : null;
    }

    /// <summary>Testo dell'intervallo QNH compatibile col parser del viewer (≥/≤/–).</summary>
    /// <summary>
    /// Fasce QNH di default per la tabella Transition Level (TL = TA + offset). Bande inclusive in hPa:
    /// QNH &lt; 977 → +2500; 977–994 → +2000; 995–1012 → +1500; QNH ≥ 1013 → +1000. Editabili per aeroporto.
    /// </summary>
    private static readonly (int? From, int? To, int OffsetFt)[] DefaultTlBands =
    {
        (null, 976, 2500),
        (977, 994, 2000),
        (995, 1012, 1500),
        (1013, null, 1000),
    };

    /// <summary>Semina la tabella TL di default (4 fasce QNH, TL = TA + offset) se l'aeroporto non ne ha ancora. Idempotente.</summary>
    private static void EnsureDefaultTransitionLevels(Airport airport)
    {
        if (airport.TransitionLevels.Count > 0) return;
        for (var i = 0; i < DefaultTlBands.Length; i++)
        {
            var band = DefaultTlBands[i];
            airport.TransitionLevels.Add(new AirportTransitionLevel
            {
                AirportId = airport.Id, Order = i, QnhFrom = band.From, QnhTo = band.To,
                Level = TransitionLevelFor(airport.TransitionAltitudeFt, band.OffsetFt),
            });
        }
    }

    /// <summary>Ricalcola il TL delle sole righe che combaciano ancora con le fasce di default (TL = TA + offset);
    /// le righe con fasce QNH personalizzate restano intatte. Idempotente.</summary>
    private static void RecomputeDefaultBandLevels(Airport airport)
    {
        foreach (var row in airport.TransitionLevels)
            if (DefaultBandOffset(row.QnhFrom, row.QnhTo) is int offset)
                row.Level = TransitionLevelFor(airport.TransitionAltitudeFt, offset);
    }

    /// <summary>Offset della fascia di default che combacia esattamente con (from,to), altrimenti null (fascia personalizzata).</summary>
    private static int? DefaultBandOffset(int? from, int? to)
    {
        foreach (var b in DefaultTlBands)
            if (b.From == from && b.To == to) return b.OffsetFt;
        return null;
    }

    /// <summary>
    /// TL per una fascia: TA + offset arrotondato al FL superiore multiplo di 5 (500 ft), es. TA 6000 + 2500 → "FL85".
    /// Se la TA non è ancora nota, restituisce la formula "TA + offset ft".
    /// </summary>
    private static string TransitionLevelFor(int? transitionAltitudeFt, int offsetFt)
    {
        if (transitionAltitudeFt is not int ta) return $"TA + {offsetFt} ft";
        var fl = (int)Math.Ceiling((ta + offsetFt) / 500.0) * 5;
        return $"FL{fl}";
    }

    private static string QnhRange(int? from, int? to) =>
        (from, to) switch
        {
            (int f, null) => $"≥ {f}",
            (null, int t) => $"≤ {t}",
            (int f, int t) => $"{f} – {t}",
            _ => "—",
        };

    /// <summary>Condizione della regola in testo: soglie coda/traverso + superficie + nome + eventuali condizioni temporali avanzate.</summary>
    private static string RuleCondition(AirportRunwayRule r)
    {
        var parts = new List<string> { $"tailwind ≤ {r.MaxTailwindKt} kt" };
        if (r.MaxCrosswindKt is int xw) parts.Add($"crosswind ≤ {xw} kt");
        if (r.Surface == RunwaySurface.Dry) parts.Add("dry runway");
        else if (r.Surface == RunwaySurface.Wet) parts.Add("wet runway");
        if (r.TimeFromLocalMin is int tf && r.TimeToLocalMin is int tt) parts.Add($"{Hhmm(tf)}–{Hhmm(tt)} LT");
        else if (r.TimeFromLocalMin is int tf2) parts.Add($"from {Hhmm(tf2)} LT");
        else if (r.TimeToLocalMin is int tt2) parts.Add($"until {Hhmm(tt2)} LT");
        if (DaysLabel(r.DaysOfWeekMask) is string dl) parts.Add(dl);
        if (r.DateParity == DateParity.Even) parts.Add("even days");
        else if (r.DateParity == DateParity.Odd) parts.Add("odd days");
        if (DateWindowLabel(r.DateFromMonthDay, r.DateToMonthDay) is string dw) parts.Add(dw);
        var cond = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(r.Name) ? cond : $"{r.Name!.Trim()}: {cond}";
    }

    private static string Hhmm(int min) => $"{min / 60:00}:{min % 60:00}";

    private static readonly string[] MonthAbbr =
        { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    /// <summary>Etichetta della finestra stagionale ricorrente (MMDD): "from 1 Jan to 31 Mar". null = nessun vincolo.</summary>
    private static string? DateWindowLabel(int? from, int? to)
    {
        if (from is null && to is null) return null;
        if (from is int f && to is int t) return $"from {Md(f)} to {Md(t)}";
        if (from is int f2) return $"from {Md(f2)}";
        return $"until {Md(to!.Value)}";

        static string Md(int mmdd) => $"{mmdd % 100} {MonthAbbr[Math.Clamp(mmdd / 100, 1, 12) - 1]}";
    }

    private static readonly string[] DayNames = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    private static string? DaysLabel(int? mask)
    {
        if (mask is not int m || m == 0 || m == 0b1111111) return null;   // null/0/tutti = nessun vincolo da mostrare
        var names = Enumerable.Range(0, 7).Where(b => (m & (1 << b)) != 0).Select(b => DayNames[b]);
        return string.Join("/", names);
    }
}
