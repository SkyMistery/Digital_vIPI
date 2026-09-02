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
                x.Circling, x.ThresholdLat, x.ThresholdLon, x.ThresholdElevationFt))
            .ToListAsync(ct);
        var rules = await _db.AirportRunwayRules.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order)
            .Select(x => new RunwayRuleRow(x.Id, x.DepRunways, x.ArrRunways, x.Name,
                x.MaxTailwindKt, x.MaxCrosswindKt, x.Surface, x.Note,
                x.TimeFromLocalMin, x.TimeToLocalMin, x.DaysOfWeekMask, x.DateParity,
                x.DateFromMonthDay, x.DateToMonthDay))
            .ToListAsync(ct);
        var sids = await _db.AirportSids.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order)
            .Select(x => new SidRow(x.Id, x.Runway, x.Fix, x.Name, x.Transition, x.InitialClimb, x.Type, x.Cat, x.Wtc, x.Condition,
                x.IsImported, x.Priority, x.StableKey, x.SourceAiracCycle, x.ForcePublished, x.NeedsFixReview, x.InitialClimbByApp))
            .ToListAsync(ct);

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
            TransitionLevels = tls, Runways = rwys, Rules = rules, Sids = sids, Links = links,
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
                    x.Circling, x.ThresholdLat, x.ThresholdLon, x.ThresholdElevationFt) })
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

    public async Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default)
    {
        var a = await _db.Airports.Include(x => x.TransitionLevels)
            .FirstOrDefaultAsync(x => x.Icao == icao, ct) ?? throw NotFound(icao);
        a.TransitionAltitudeFt = ta;
        RecomputeDefaultBandLevels(a);
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

    public async Task SaveSidsAsync(string icao, IReadOnlyList<SidRow> rows, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        // Origin-aware: sostituisce SOLO le righe manuali; le importate (IsImported=true) restano intatte.
        _db.AirportSids.RemoveRange(_db.AirportSids.Where(x => x.AirportId == id && !x.IsImported));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            _db.AirportSids.Add(new AirportSid
            {
                AirportId = id, Order = i, Runway = r.Runway, Fix = r.Fix.Trim(), Name = r.Name.Trim(),
                Transition = r.Transition, InitialClimb = r.InitialClimb, InitialClimbByApp = r.InitialClimbByApp,
                Type = r.Type, Cat = r.Cat, Wtc = r.Wtc, Condition = r.Condition,
                // La priorità fra SID dello stesso punto vale anche per le righe a mano: la colonna esisteva
                // già (tabella unica con le importate), ma qui non veniva scritta e si perdeva a ogni salvataggio.
                Priority = r.Priority,
                IsImported = false,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReplaceImportedSidsAsync(string icao, IReadOnlyList<ImportedSid> rows, string airacCycle, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        // Snapshot per StableKey di TUTTE le righe (manuali + importate): serve a riapplicare priorità/forzatura,
        // il fix risolto a mano e il PRIMO ciclo d'entrata alle righe con StableKey coincidente.
        //
        // First-wins sulla chiave, in ordine di Id. La StableKey esclude di proposito la cifra della revisione,
        // quindi un file .sid che contiene DUE revisioni della stessa SID (es. ROBO1H e ROBO2H) produce due righe
        // con la stessa chiave: costruire qui un dizionario a chiave unica lanciava «An item with the same key has
        // already been added» al primo REIMPORT di quell'aeroporto. Il primo import passava (tabella vuota, nessuna
        // chiave da indicizzare) e ogni successivo fallliva, quindi l'import restava rotto per sempre su quegli
        // scali — in silenzio, perché il job periodico logga l'errore per-ICAO a Debug. Misurato sul DB di
        // sviluppo: 20 coppie così su 1478 righe, tra cui LIRF, LIMC, LIME, LIBG, LIED, LIEO, LIPQ.
        var priorRows = await _db.AirportSids.AsNoTracking()
            .Where(x => x.AirportId == id && x.StableKey != null)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var prior = new Dictionary<string, PriorSid>();
        foreach (var x in priorRows)
            prior.TryAdd(x.StableKey!,
                new PriorSid(x.Priority, x.ForcePublished, x.SourceAiracCycle, x.Fix, x.NeedsFixReview, x.Name, x.Transition, x.Type,
                    x.InitialClimb, x.Cat, x.Wtc, x.Condition, x.InitialClimbByApp));

        _db.AirportSids.RemoveRange(_db.AirportSids.Where(x => x.AirportId == id && x.IsImported));

        var baseOrder = 1000;   // le importate dopo le manuali; l'ordine di resa reale è per fix/priorità nel viewer
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var found = prior.TryGetValue(r.StableKey, out var p);

            // `airacCycle` è il ciclo DAL QUALE la riga vale, deciso da SidStampCycle su quel che la sorgente
            // dichiara (carta §AW2). Se il contenuto è invariato dall'import precedente si conserva il PRIMO:
            // così, raggiunto quel ciclo, la SID diventa pubblica (IsPublicAt) e ci RESTA. Solo un contenuto
            // cambiato — una revisione nuova — riparte dal ciclo d'entrata appena calcolato.
            var sourceCycle = found && ContentUnchanged(p!, r) ? (p!.SourceAiracCycle ?? airacCycle) : airacCycle;

            // Se la sorgente ripropone il prefisso grezzo (NeedsFixReview) ma quel fix era già stato risolto a mano,
            // conserva la risoluzione invece di ripristinare il grezzo a ogni reimport.
            var fix = r.Fix.Trim();
            var needsReview = r.NeedsFixReview;
            if (found && r.NeedsFixReview && !p!.NeedsFixReview && !string.IsNullOrWhiteSpace(p.Fix))
            {
                fix = p.Fix!.Trim();
                needsReview = false;
            }

            _db.AirportSids.Add(new AirportSid
            {
                AirportId = id, Order = baseOrder + i, Runway = r.Runway, Fix = fix, Name = r.Name.Trim(),
                Transition = r.Transition, Type = r.Type,
                IsImported = true, StableKey = r.StableKey, SourceAiracCycle = sourceCycle,
                NeedsFixReview = needsReview,
                Priority = p?.Priority, ForcePublished = p?.ForcePublished ?? false,
                // Arricchimenti editoriali sovrapposti a mano: sopravvivono al reimport (la sorgente non li fornisce).
                InitialClimb = p?.InitialClimb, InitialClimbByApp = p?.InitialClimbByApp ?? false,
                Cat = p?.Cat, Wtc = p?.Wtc, Condition = p?.Condition,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    // "Contenuto invariato" = stessi campi che definiscono la SID lato sorgente (codice con revisione, transition, tipo).
    // Fix/pista fanno parte della StableKey, quindi qui non si riconfrontano.
    private static bool ContentUnchanged(PriorSid p, ImportedSid r) =>
        string.Equals(p.Name, r.Name.Trim(), StringComparison.Ordinal)
        && string.Equals(p.Transition ?? "", r.Transition ?? "", StringComparison.Ordinal)
        && string.Equals(p.Type ?? "", r.Type ?? "", StringComparison.Ordinal);

    // Snapshot dell'import precedente per StableKey (materializzato client-side da ToDictionaryAsync).
    private sealed record PriorSid(int? Priority, bool ForcePublished, string? SourceAiracCycle,
        string? Fix, bool NeedsFixReview, string Name, string? Transition, string? Type,
        string? InitialClimb, string? Cat, string? Wtc, string? Condition, bool InitialClimbByApp);

    public async Task UpdateImportedSidAsync(int sidId, int? priority, bool forcePublished, string? resolvedFix,
        string? initialClimb, bool initialClimbByApp, string? cat, string? wtc, string? condition, CancellationToken ct = default)
    {
        var s = await _db.AirportSids.FirstOrDefaultAsync(x => x.Id == sidId && x.IsImported, ct);
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

    public async Task MergeFromSourceAsync(string icao, int? transitionAltitude,
        IReadOnlyList<SourceRunway> runways, CancellationToken ct = default)
    {
        var airport = await _db.Airports.Include(a => a.Runways).Include(a => a.TransitionLevels)
            .FirstOrDefaultAsync(a => a.Icao == icao, ct) ?? throw NotFound(icao);

        if (transitionAltitude is int ta) airport.TransitionAltitudeFt = ta;

        var nextOrder = airport.Runways.Count == 0 ? 0 : airport.Runways.Max(r => r.Order) + 1;
        foreach (var rw in runways)
        {
            var ident = rw.Ident.Trim().ToUpperInvariant();
            var ex = airport.Runways.FirstOrDefault(r => string.Equals(r.Ident, ident, StringComparison.OrdinalIgnoreCase));
            if (ex is not null)
            {
                ex.LengthM = rw.LengthM;            // sovrascrive solo i campi IVAO
                ex.Bearing = rw.Bearing ?? BearingFromIdent(ident) ?? ex.Bearing;
                Soglia(ex, rw);
            }
            else
            {
                var nuova = new AirportRunway
                {
                    AirportId = airport.Id, Order = nextOrder++, Ident = ident,
                    LengthM = rw.LengthM, Bearing = rw.Bearing ?? BearingFromIdent(ident),
                };
                Soglia(nuova, rw);
                airport.Runways.Add(nuova);
            }
        }

        // Tabella Transition Level standard (TL = TA + margine per fascia QNH) se non ancora impostata.
        EnsureDefaultTransitionLevels(airport);
        // Con TA di sorgente (bottone "Salva TA" bloccato) questo è l'unico path che aggiorna la TA: ricalcola
        // qui le righe di fascia-default già esistenti, altrimenti resterebbero sull'ultima TA (o "TA + N ft").
        RecomputeDefaultBandLevels(airport);

        await _db.SaveChangesAsync(ct);
    }

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
                nasceLive: BornLive, conSegnaposto: false);
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
    /// <summary>Sezioni derivate che nascono Live: il meteo (mai congelabile) e le SID (scelta editoriale storica).</summary>
    private static bool BornLive(string key) =>
        SectionCatalog.IsAlwaysLive(key) || string.Equals(key, "sids", StringComparison.OrdinalIgnoreCase);

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
                a.HasMilitaryPresence, a.IsMilitaryOnly, a.DocumentId, a.MilDocumentId))
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
