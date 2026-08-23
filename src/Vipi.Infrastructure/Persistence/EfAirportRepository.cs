using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;

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

    /// <summary>Titoli delle sezioni del documento gestite (rigenerate); le altre vengono preservate. Include sia i
    /// titoli EN correnti sia quelli IT legacy, così un rebuild di un documento vecchio rimuove le sezioni italiane
    /// (e le rigenera in inglese) invece di lasciarle duplicate.</summary>
    private static readonly string[] ManagedSectionTitles =
    {
        // EN correnti
        "Runway rules", "Transition levels", "Frequencies", "Runways", "SID",
        // IT legacy (documenti generati prima dell'i18n)
        "Configurazioni pista", "Regole piste", "Quote di transizione", "Frequenze", "Piste",
    };

    /// <summary>Chiave delle sezioni editoriali libere dell'aeroporto emesse nel documento dal profilo (doc 08e-airport):
    /// hanno titolo arbitrario, quindi si riconoscono/rimuovono per chiave (non per titolo come le managed).</summary>
    private const string ExtraSectionKey = "airportextra";

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
            .Select(x => new RunwayRow(x.Id, x.Ident, x.LengthM, x.Bearing, x.ToraM, x.LdaM, x.AppProcedures, x.Patterns, x.Circling))
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

        var extras = await _db.AirportExtraSections.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order).Select(x => new ExtraSectionRow(x.Id, x.Title, x.Body)).ToListAsync(ct);

        return new AirportData
        {
            AirportId = airport.Id, Icao = airport.Icao, Name = airport.Name, AccCode = airport.Acc!.Code,
            TransitionAltitudeFt = airport.TransitionAltitudeFt,
            TransitionLevels = tls, Runways = rwys, Rules = rules, Sids = sids, Links = links, ExtraSections = extras,
        };
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

    public async Task SaveRunwaysAsync(string icao, IReadOnlyList<RunwayRow> rows, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        _db.AirportRunways.RemoveRange(_db.AirportRunways.Where(x => x.AirportId == id));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            _db.AirportRunways.Add(new AirportRunway
            {
                AirportId = id, Order = i, Ident = r.Ident.Trim().ToUpperInvariant(), LengthM = r.LengthM, Bearing = r.Bearing,
                ToraM = r.ToraM, LdaM = r.LdaM, AppProcedures = r.AppProcedures, Patterns = r.Patterns, Circling = r.Circling,
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
        // il fix risolto a mano e il ciclo di PRIMO prelievo alle righe con StableKey coincidente.
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

            // Il ciclo-sorgente è la data di PRIMO prelievo. Se il contenuto è invariato dall'import precedente,
            // conserva quel ciclo: così, superato il ciclo, la SID diventa pubblica (IsPublicAt) e ci RESTA. Solo un
            // contenuto cambiato (nuova revisione) riparte dal ciclo corrente, riottenendo il buffer di un ciclo.
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

    public async Task SaveExtraSectionsAsync(string icao, IReadOnlyList<ExtraSectionRow> rows, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);

        // Qui la rimozione di una foto e' una RISCRITTURA: l'editor rimanda tutte le sezioni, e chi tolga un blocco
        // immagine lo fa semplicemente non rispedendolo. Per sapere quali foto hanno perso il loro blocco si
        // confronta il prima col dopo; a decidere se cancellarle resta comunque DeleteOrphansAsync.
        var precedenti = await _db.AirportExtraSections.Where(x => x.AirportId == id).ToListAsync(ct);
        var shaPrima = Vipi.Application.Media.MediaReferenceScanner.ScanAll(precedenti.Select(x => x.Body));

        _db.AirportExtraSections.RemoveRange(precedenti);
        var order = 0;
        foreach (var r in rows.Where(r => !string.IsNullOrWhiteSpace(r.Title)))
            _db.AirportExtraSections.Add(new AirportExtraSection
            {
                AirportId = id, Order = order++, Title = r.Title.Trim(),
                Body = string.IsNullOrWhiteSpace(r.Body) ? null : r.Body!.Trim(),
            });
        await _db.SaveChangesAsync(ct);

        shaPrima.ExceptWith(Vipi.Application.Media.MediaReferenceScanner.ScanAll(rows.Select(r => r.Body)));
        if (shaPrima.Count > 0) await _media.DeleteOrphansAsync(shaPrima.ToList(), ct);
    }

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
        IReadOnlyList<(string Ident, int? LengthM, int? Bearing)> runways, CancellationToken ct = default)
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
            }
            else
            {
                airport.Runways.Add(new AirportRunway
                {
                    AirportId = airport.Id, Order = nextOrder++, Ident = ident,
                    LengthM = rw.LengthM, Bearing = rw.Bearing ?? BearingFromIdent(ident),
                });
            }
        }

        // Tabella Transition Level standard (TL = TA + margine per fascia QNH) se non ancora impostata.
        EnsureDefaultTransitionLevels(airport);
        // Con TA di sorgente (bottone "Salva TA" bloccato) questo è l'unico path che aggiorna la TA: ricalcola
        // qui le righe di fascia-default già esistenti, altrimenti resterebbero sull'ultima TA (o "TA + N ft").
        RecomputeDefaultBandLevels(airport);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> RebuildDocumentAsync(string icao, CancellationToken ct = default)
    {
        var airport = await _db.Airports
            .Include(a => a.TransitionLevels).Include(a => a.Runways).Include(a => a.RunwayRules)
            .FirstOrDefaultAsync(a => a.Icao == icao, ct) ?? throw NotFound(icao);

        // Garantisce la tabella TL di default anche per aeroporti generati senza import IVAO (es. TA/TL mai popolate).
        EnsureDefaultTransitionLevels(airport);
        // Risolve i livelli delle fasce-default se la TA è nota ma le righe portano ancora il placeholder "TA + N ft"
        // (seminate quando la TA non era ancora arrivata dalla sorgente): senza questo il rebuild pubblicherebbe i
        // placeholder invece dei FL calcolati. Le fasce personalizzate restano intatte.
        RecomputeDefaultBandLevels(airport);

        // Solo i settori-FOGLIA dell'aeroporto (DEL/GND/TWR/ITwr) appartengono alla vIPI d'aeroporto.
        // Gli APP NON ci vanno mai: se sono "di ACC" stanno nella vIPI di ACC, se standalone hanno doc proprio.
        // Ordino per (int)Type in MEMORIA: Type è un enum salvato come stringa, quindi ORDER BY (int)Type in SQL
        // genera CAST("Type" AS integer) → su Postgres 'Twr'→integer lancia 22P02 (su SQLite tornava 0 in silenzio).
        var sectors = (await _db.Sectors.Where(s => s.AirportId == airport.Id && s.Type != SectorType.App)
            .ToListAsync(ct))
            .OrderBy(s => (int)s.Type).ToList();
        var links = await _db.AirportFrequencyLinks.Where(x => x.AirportId == airport.Id).OrderBy(x => x.Order)
            .Include(x => x.SourceSector).Where(x => x.SourceSector != null && x.SourceSector!.DefaultFrequency != null).ToListAsync(ct);

        var now = DateTime.UtcNow;
        var cycle = new AiracService().GetCycle(now);

        // Documento esistente (via settori) o nuovo documento pubblicato.
        var docId = sectors.Where(s => s.DocumentId != null).Select(s => s.DocumentId).FirstOrDefault();
        Document doc;
        DocumentVersion ver;
        // RenderMode editoriale della sezione SID (doc 10 §3e/§S4c): preservato tra i rebuild (la sezione è rigenerata,
        // ma la scelta Live/Frozen dello staff no). Default Live alla prima generazione.
        var sidsMode = RenderMode.Live;
        if (docId is int existing)
        {
            doc = await _db.Documents.Include(d => d.Versions).FirstAsync(d => d.Id == existing, ct);
            ver = await _db.DocumentVersions.Include(v => v.Sections).ThenInclude(s => s.Blocks)
                .FirstAsync(v => v.Id == doc.CurrentVersionId, ct);
            // Rimuove le sezioni gestite (per titolo) + le sezioni editoriali libere dal profilo (per chiave); preserva
            // eventuali sezioni aggiunte a mano di altra natura.
            var managed = ver.Sections
                .Where(s => ManagedSectionTitles.Contains(s.Title) || s.SectionKey == ExtraSectionKey).ToList();
            sidsMode = ver.Sections.FirstOrDefault(s => s.SectionKey == "sids")?.RenderMode ?? RenderMode.Live;
            foreach (var s in managed) _db.ContentBlocks.RemoveRange(s.Blocks);
            _db.DocumentSections.RemoveRange(managed);
        }
        else
        {
            // Alla prima generazione il documento resta in BOZZA: l'aeroporto appena importato non è ancora
            // pubblico. Sarà lo staff a pubblicarlo a mano da /services/vsop/versioni. (I rebuild successivi — ramo
            // "documento esistente" sopra — preservano lo stato: un doc già pubblicato resta pubblicato.)
            doc = new Document
            {
                Type = DocumentType.Vipi, Title = $"vIPI — {icao} {airport.Name}", Language = Language.It,
                Status = DocumentStatus.Draft, LastUpdatedUtc = now, LastUpdatedAiracCycle = cycle,
            };
            ver = new DocumentVersion
            {
                Document = doc, VersionNumber = 1, Status = DocumentStatus.Draft,
                CreatedByUserId = 0, CreatedUtc = now, AiracCycle = cycle, Note = "Generato dal profilo aeroporto",
            };
            doc.Versions.Add(ver);
            _db.Documents.Add(doc);
            await _db.SaveChangesAsync(ct);
            doc.CurrentVersionId = ver.Id;
            var primary = sectors.FirstOrDefault(s => IsTower(s.Type)) ?? sectors.FirstOrDefault();
            foreach (var s in sectors) { s.DocumentId = doc.Id; s.IsPrimary = s == primary; }
        }

        // Correzione/idempotenza: sgancia eventuali APP di questo aeroporto erroneamente legati a questa vIPI
        // d'aeroporto (binding storico). Da qui in poi torneranno selezionabili in «Nuovo documento».
        var strayApps = await _db.Sectors
            .Where(s => s.AirportId == airport.Id && s.Type == SectorType.App && s.DocumentId == doc.Id)
            .ToListAsync(ct);
        foreach (var s in strayApps) { s.DocumentId = null; s.IsPrimary = false; }

        var b = new DocBuilder(_db, ver);
        var order = 0;

        // 1 — Regole piste (solo se presenti). Scelta in base a vento in coda/traverso + superficie.
        if (airport.RunwayRules.Count > 0)
        {
            var sec = b.Section("Runway rules", BlockSection.Airport, ++order);
            b.Prose(sec, BlockTier.Reduced,
                "The **first** rule whose conditions are met applies (tailwind/crosswind within the stated limits and " +
                "matching surface); if none applies, the runway with the best headwind is used.");
            b.Table(sec, BlockTier.Reduced, new
            {
                columns = new[] { "Condition", "DEP", "ARR", "Notes" },
                unified = false,
                rows = airport.RunwayRules.OrderBy(r => r.Order)
                    .Select(r => (object)new { cells = new[] { RuleCondition(r), Dash(r.DepRunways), Dash(r.ArrRunways), r.Note ?? "—" } })
                    .ToList(),
            });
        }

        // 2 — Transition levels (TA + tabella TL).
        var trans = b.Section("Transition levels", BlockSection.Airport, ++order);
        b.Prose(trans, BlockTier.Reduced, airport.TransitionAltitudeFt is int taFt
            ? $"**Transition Altitude:** {taFt} ft" : "**Transition Altitude:** _to be defined_");
        if (airport.TransitionLevels.Count > 0)
            b.Table(trans, BlockTier.Reduced, new
            {
                columns = new[] { "QNH (hPa)", "Transition Level" },
                unified = false,
                rows = airport.TransitionLevels.OrderBy(t => t.Order)
                    .Select(t => (object)new { cells = new[] { QnhRange(t.QnhFrom, t.QnhTo), t.Level } }).ToList(),
            });

        // 3 — Frequenze (dal catalogo AirportSector: ATIS·DEL·GND·TWR·APP/DEP, ★ = principale per tipo) + link risolti.
        var catalog = await _db.AirportSectors.AsNoTracking()
            .Where(s => s.AirportIcao == icao && !s.IsHidden && s.Frequency != null)
            .ToListAsync(ct);
        var freqRows = new List<object>();
        foreach (var s in catalog.OrderBy(FreqOrder).ThenByDescending(s => s.IsPrimary).ThenBy(s => s.ComposePosition))
        {
            var cells = new[] { FreqNameForPosition(s.Position), s.ComposePosition, s.Frequency! };
            freqRows.Add(s.IsPrimary ? new { primary = true, star = true, cells } : (object)new { cells });
        }
        foreach (var l in links)
            freqRows.Add(new { cells = new[] { l.LabelOverride ?? l.SourceSector!.Callsign, l.SourceSector!.Callsign, l.SourceSector!.DefaultFrequency! } });
        var freq = b.Section("Frequencies", BlockSection.Frequencies, ++order);
        b.Table(freq, BlockTier.Reduced, new { columns = new[] { "Name", "Callsign", "Frequency" }, unified = false, rows = freqRows });

        // 4 — Runways.
        var rwy = b.Section("Runways", BlockSection.Airport, ++order);
        b.Table(rwy, BlockTier.Extended, new
        {
            columns = new[] { "Runway", "TORA", "LDA", "APP procedures", "Patterns", "Circling" },
            unified = false,
            rows = airport.Runways.OrderBy(r => r.Order).Select(r => (object)new
            {
                cells = new[]
                {
                    r.Ident,
                    r.ToraM ?? (r.LengthM is int m ? $"{m} m" : "—"),
                    r.LdaM ?? (r.LengthM is int m2 ? $"{m2} m" : "—"),
                    Dash(r.AppProcedures), Dash(r.Patterns), Dash(r.Circling),
                }
            }).ToList(),
        });

        // 5 — SID: sezione DERIVABILE (doc 10 §3e), non più «cotta» qui. Il merge editoriali+importate (filtro AIRAC)
        // e l'ordine per FIX/priorità si derivano a view-time (AirportSidDerivationService); il viewer li rende live.
        // La sezione resta come ancora del RenderMode (default Live) e portante per la cattura di release quando Frozen.
        var sid = b.Section("SID", "sids", ++order);
        sid.RenderMode = sidsMode;   // preserva la scelta editoriale Live/Frozen tra i rebuild (doc 10 §S4c)

        // 6 — Sezioni editoriali libere del profilo (titolo + prosa markdown), keyed così da entrare nel documento
        // (e quindi nello snapshot di release) invece di restare in uno store parallelo. Doc 08e-airport.
        var extras = await _db.AirportExtraSections.Where(x => x.AirportId == airport.Id).OrderBy(x => x.Order).ToListAsync(ct);
        foreach (var x in extras)
        {
            var sec = b.Section(string.IsNullOrWhiteSpace(x.Title) ? "Section" : x.Title, ExtraSectionKey, ++order);
            // I blocchi editoriali (Prosa/Callout/Tabella) sono serializzati nel Body (formato condiviso col vIPI editor);
            // un Body legacy markdown viene letto come un singolo blocco prosa (ExtraBlocks.Parse).
            foreach (var blk in ExtraBlocks.Parse(x.Body))
            {
                switch (blk.Format)
                {
                    case BlockFormat.Callout when !string.IsNullOrWhiteSpace(blk.Text):
                        b.Callout(sec, blk.CalloutKind, "", BlockTier.Extended, blk.Text!);
                        break;
                    case BlockFormat.Table when !string.IsNullOrWhiteSpace(blk.TableJson):
                        b.TableRaw(sec, BlockTier.Extended, blk.TableJson!);
                        break;
                    case BlockFormat.Image when MediaRef.Parse(blk.ImageJson) is not null:
                        // Senza questo ramo l'immagine resterebbe nel profilo e sparirebbe dal documento pubblicato:
                        // il viewer legge il documento «cotto», non gli extra.
                        b.Image(sec, BlockTier.Extended, blk.ImageJson!, blk.Text);
                        break;
                    case BlockFormat.Prose or BlockFormat.List when !string.IsNullOrWhiteSpace(blk.Text):
                        b.Prose(sec, BlockTier.Extended, blk.Text!);
                        break;
                }
            }
        }

        doc.LastUpdatedUtc = now;
        doc.LastUpdatedAiracCycle = cycle;
        await _db.SaveChangesAsync(ct);
        return doc.Id;
    }

    public async Task<RenderMode> GetSidsRenderModeAsync(string icao, CancellationToken ct = default)
    {
        var sec = await CurrentSidsSectionAsync(icao, ct);
        return sec?.RenderMode ?? RenderMode.Live;
    }

    public async Task SetSidsRenderModeAsync(string icao, RenderMode mode, CancellationToken ct = default)
    {
        var sec = await CurrentSidsSectionAsync(icao, ct);
        if (sec is null) return;   // documento/sezione non ancora generati: nasceranno al primo rebuild (default Live)
        sec.RenderMode = mode;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int?> GetDocumentIdAsync(string icao, CancellationToken ct = default)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        // Solo settori che identificano il documento d'AEROPORTO: l'APP standalone dello stesso ICAO ha il suo.
        return await _db.Sectors.AsNoTracking().AirportDocSectors()
            .Where(s => s.AirportIcao == icao && s.DocumentId != null)
            .Select(s => s.DocumentId)
            .FirstOrDefaultAsync(ct);
    }

    // Sezione "sids" della versione CORRENTE del documento dell'aeroporto (tracciata: settabile). Null se assente.
    private async Task<DocumentSection?> CurrentSidsSectionAsync(string icao, CancellationToken ct)
    {
        icao = (icao ?? "").Trim().ToUpperInvariant();
        var verId = await _db.Sectors.AirportDocSectors()
            .Where(s => s.AirportIcao == icao && s.DocumentId != null)
            .Select(s => s.Document!.CurrentVersionId)
            .FirstOrDefaultAsync(ct);
        if (verId is not int vid) return null;
        return await _db.DocumentSections
            .FirstOrDefaultAsync(s => s.DocumentVersionId == vid && s.SectionKey == "sids", ct);
    }

    // ---- helper ----

    private async Task<int> AirportIdAsync(string icao, CancellationToken ct) =>
        await _db.Airports.Where(a => a.Icao == icao).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw NotFound(icao);

    private static InvalidOperationException NotFound(string icao) => new($"Aeroporto {icao} inesistente.");

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

    /// <summary>Builder minimale per il documento di aeroporto (stesse convenzioni di RomaAirportSeed/DocBuilder).</summary>
    private sealed class DocBuilder
    {
        private readonly VipiDbContext _db;
        private readonly DocumentVersion _ver;
        public DocBuilder(VipiDbContext db, DocumentVersion ver) { _db = db; _ver = ver; }

        public DocumentSection Section(string title, BlockSection kind, int order) =>
            Section(title, SectionCatalogBridge.KeyFor(kind) ?? SectionKeys.NewCustom(), order);

        public DocumentSection Section(string title, string sectionKey, int order)
        {
            var s = new DocumentSection
            {
                DocumentVersion = _ver, ParentSection = null, Title = title, Order = order,
                Depth = 0, SectionKey = sectionKey, RowVersion = Guid.NewGuid().ToByteArray(),
            };
            _ver.Sections.Add(s);
            _db.DocumentSections.Add(s);
            return s;
        }

        public void Prose(DocumentSection s, BlockTier tier, string markdown) =>
            Add(s, BlockFormat.Prose, tier, body: markdown);

        public void Callout(DocumentSection s, CalloutKind kind, string title, BlockTier tier, string markdown) =>
            Add(s, BlockFormat.Callout, tier, body: markdown, callout: kind,
                bodyJson: JsonSerializer.Serialize(new { title }));

        public void Table(DocumentSection s, BlockTier tier, object data) =>
            Add(s, BlockFormat.Table, tier, bodyJson: JsonSerializer.Serialize(data));

        /// <summary>Tabella con BodyJson già serializzato (columns/rows) — usato dai blocchi extra a formato condiviso.</summary>
        public void TableRaw(DocumentSection s, BlockTier tier, string bodyJson) =>
            Add(s, BlockFormat.Table, tier, bodyJson: bodyJson);

        /// <summary>Immagine: <paramref name="imageJson"/> è il riferimento (<see cref="MediaRef"/>), il corpo la didascalia.</summary>
        public void Image(DocumentSection s, BlockTier tier, string imageJson, string? caption) =>
            Add(s, BlockFormat.Image, tier, body: caption, bodyJson: imageJson);

        private void Add(DocumentSection s, BlockFormat format, BlockTier tier,
            string? body = null, string? bodyJson = null, CalloutKind? callout = null)
        {
            var block = new ContentBlock
            {
                DocumentVersion = _ver, Section = s, Order = s.Blocks.Count + 1,
                Tier = tier, Format = format, Visibility = BlockVisibility.Always, CalloutKind = callout,
                Body = body, BodyJson = bodyJson, RowVersion = Guid.NewGuid().ToByteArray(),
            };
            s.Blocks.Add(block);
            _ver.Blocks.Add(block);
            _db.ContentBlocks.Add(block);
        }
    }
}
