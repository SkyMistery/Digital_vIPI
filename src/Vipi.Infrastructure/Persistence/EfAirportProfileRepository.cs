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
public sealed class EfAirportProfileRepository : IAirportProfileRepository
{
    private readonly VipiDbContext _db;
    public EfAirportProfileRepository(VipiDbContext db) => _db = db;

    /// <summary>Titoli delle sezioni del documento gestite (rigenerate); le altre vengono preservate.</summary>
    private static readonly string[] ManagedSectionTitles =
        { "Regole piste", "Quote di transizione", "Frequenze", "Piste", "SID" };

    public async Task<string?> GetAccCodeByIcaoAsync(string icao, CancellationToken ct = default) =>
        await _db.Airports.Where(a => a.Icao == icao).Select(a => a.Acc!.Code).FirstOrDefaultAsync(ct);

    public async Task<AirportProfileData?> LoadAsync(string icao, CancellationToken ct = default)
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
            .Select(x => new RunwayRuleRow(x.Id, x.WindDirFrom, x.WindDirTo, x.WindSpeedMin, x.WindSpeedMax,
                x.Rain, x.Snow, x.DepRunways, x.ArrRunways, x.Note,
                x.TimeFromUtcMin, x.TimeToUtcMin, x.DaysOfWeekMask, x.DateParity))
            .ToListAsync(ct);
        var sids = await _db.AirportSids.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order)
            .Select(x => new SidRow(x.Id, x.Runway, x.Fix, x.Name, x.Transition, x.InitialClimb, x.Type, x.Cat, x.Wtc, x.Condition))
            .ToListAsync(ct);

        // Frequenze proprie: dai settori d'aeroporto (DEL/GND/TWR/APP), in ordine di tipo.
        var sectors = await _db.Sectors.AsNoTracking()
            .Where(s => s.AirportId == airport.Id && s.DefaultFrequency != null)
            .OrderBy(s => (int)s.Type).ToListAsync(ct);
        var own = sectors.Select(s => new OwnFrequencyRow(s.Type, FreqName(s.Type, s.Callsign), s.Callsign,
            s.DefaultFrequency!, IsTower(s.Type))).ToList();

        // Link (riferimento vivo): valore risolto ora dalla Frequency sorgente.
        var links = await _db.AirportFrequencyLinks.AsNoTracking().Where(x => x.AirportId == airport.Id)
            .OrderBy(x => x.Order).Include(x => x.SourceFrequency)
            .Where(x => x.SourceFrequency != null)
            .Select(x => new FrequencyLinkRow(x.Id, x.SourceFrequencyId,
                x.LabelOverride ?? x.SourceFrequency!.Label, x.SourceFrequency!.Callsign, x.SourceFrequency!.FrequencyMhz))
            .ToListAsync(ct);

        return new AirportProfileData
        {
            AirportId = airport.Id, Icao = airport.Icao, Name = airport.Name, AccCode = airport.Acc!.Code,
            TransitionAltitudeFt = airport.TransitionAltitudeFt, AtisFrequency = airport.AtisFrequency,
            TransitionLevels = tls, Runways = rwys, Rules = rules, Sids = sids, OwnFrequencies = own, Links = links,
        };
    }

    public async Task<IReadOnlyList<LinkableFrequencyRow>> ListLinkableFrequenciesAsync(CancellationToken ct = default) =>
        await _db.Frequencies.AsNoTracking().Include(f => f.Sector!)
            .OrderBy(f => f.Sector!.AirportIcao).ThenBy(f => f.Callsign)
            .Select(f => new LinkableFrequencyRow(f.Id, f.Sector!.AirportIcao, f.Sector!.Callsign,
                f.Label, f.Callsign, f.FrequencyMhz))
            .ToListAsync(ct);

    public async Task SetTransitionAltitudeAsync(string icao, int? ta, CancellationToken ct = default)
    {
        var a = await _db.Airports.Include(x => x.TransitionLevels)
            .FirstOrDefaultAsync(x => x.Icao == icao, ct) ?? throw NotFound(icao);
        a.TransitionAltitudeFt = ta;
        // Ricalcola il TL delle sole righe che corrispondono ancora alle fasce di default (TL = TA + offset);
        // le righe con fasce QNH personalizzate restano intatte.
        foreach (var row in a.TransitionLevels)
            if (DefaultBandOffset(row.QnhFrom, row.QnhTo) is int offset)
                row.Level = TransitionLevelFor(ta, offset);
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
                AirportId = id, Order = i, WindDirFrom = r.WindDirFrom, WindDirTo = r.WindDirTo,
                WindSpeedMin = r.WindSpeedMin, WindSpeedMax = r.WindSpeedMax, Rain = r.Rain, Snow = r.Snow,
                DepRunways = (r.DepRunways ?? "").Trim(), ArrRunways = (r.ArrRunways ?? "").Trim(), Note = r.Note,
                TimeFromUtcMin = r.TimeFromUtcMin, TimeToUtcMin = r.TimeToUtcMin,
                DaysOfWeekMask = r.DaysOfWeekMask, DateParity = r.DateParity,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveSidsAsync(string icao, IReadOnlyList<SidRow> rows, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        _db.AirportSids.RemoveRange(_db.AirportSids.Where(x => x.AirportId == id));
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            _db.AirportSids.Add(new AirportSid
            {
                AirportId = id, Order = i, Runway = r.Runway, Fix = r.Fix.Trim(), Name = r.Name.Trim(),
                Transition = r.Transition, InitialClimb = r.InitialClimb, Type = r.Type, Cat = r.Cat, Wtc = r.Wtc, Condition = r.Condition,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveFrequencyLinksAsync(string icao, IReadOnlyList<int> sourceFrequencyIds, CancellationToken ct = default)
    {
        var id = await AirportIdAsync(icao, ct);
        _db.AirportFrequencyLinks.RemoveRange(_db.AirportFrequencyLinks.Where(x => x.AirportId == id));
        var valid = await _db.Frequencies.Where(f => sourceFrequencyIds.Contains(f.Id)).Select(f => f.Id).ToListAsync(ct);
        var order = 0;
        foreach (var fid in sourceFrequencyIds.Where(valid.Contains))
            _db.AirportFrequencyLinks.Add(new AirportFrequencyLink { AirportId = id, Order = order++, SourceFrequencyId = fid });
        await _db.SaveChangesAsync(ct);
    }

    public async Task MergeFromSourceAsync(string icao, int? transitionAltitude, string? atisFrequency,
        IReadOnlyList<(string Ident, int? LengthM, int? Bearing)> runways, CancellationToken ct = default)
    {
        var airport = await _db.Airports.Include(a => a.Runways).Include(a => a.TransitionLevels)
            .FirstOrDefaultAsync(a => a.Icao == icao, ct) ?? throw NotFound(icao);

        if (transitionAltitude is int ta) airport.TransitionAltitudeFt = ta;
        if (!string.IsNullOrWhiteSpace(atisFrequency)) airport.AtisFrequency = atisFrequency;

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

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> RebuildDocumentAsync(string icao, CancellationToken ct = default)
    {
        var airport = await _db.Airports
            .Include(a => a.TransitionLevels).Include(a => a.Runways).Include(a => a.RunwayRules).Include(a => a.Sids)
            .FirstOrDefaultAsync(a => a.Icao == icao, ct) ?? throw NotFound(icao);

        // Garantisce la tabella TL di default anche per aeroporti generati senza import IVAO (es. TA/TL mai popolate).
        EnsureDefaultTransitionLevels(airport);

        var sectors = await _db.Sectors.Where(s => s.AirportId == airport.Id).OrderBy(s => (int)s.Type).ToListAsync(ct);
        var links = await _db.AirportFrequencyLinks.Where(x => x.AirportId == airport.Id).OrderBy(x => x.Order)
            .Include(x => x.SourceFrequency).Where(x => x.SourceFrequency != null).ToListAsync(ct);

        var now = DateTime.UtcNow;
        var cycle = new AiracService().GetCycle(now);

        // Documento esistente (via settori) o nuovo documento pubblicato.
        var docId = sectors.Where(s => s.DocumentId != null).Select(s => s.DocumentId).FirstOrDefault();
        Document doc;
        DocumentVersion ver;
        if (docId is int existing)
        {
            doc = await _db.Documents.Include(d => d.Versions).FirstAsync(d => d.Id == existing, ct);
            ver = await _db.DocumentVersions.Include(v => v.Sections).ThenInclude(s => s.Blocks)
                .FirstAsync(v => v.Id == doc.CurrentVersionId, ct);
            // Rimuove le sole sezioni gestite (preserva eventuali sezioni aggiunte a mano).
            var managed = ver.Sections.Where(s => ManagedSectionTitles.Contains(s.Title)).ToList();
            foreach (var s in managed) _db.ContentBlocks.RemoveRange(s.Blocks);
            _db.DocumentSections.RemoveRange(managed);
        }
        else
        {
            doc = new Document
            {
                Type = DocumentType.Vipi, Title = $"vIPI — {icao} {airport.Name}", Language = Language.It,
                Status = DocumentStatus.Published, LastUpdatedUtc = now, LastUpdatedAiracCycle = cycle,
            };
            ver = new DocumentVersion
            {
                Document = doc, VersionNumber = 1, Status = DocumentStatus.Published,
                CreatedByUserId = 0, CreatedUtc = now, AiracCycle = cycle, Note = "Generato dal profilo aeroporto",
            };
            doc.Versions.Add(ver);
            _db.Documents.Add(doc);
            await _db.SaveChangesAsync(ct);
            doc.CurrentVersionId = ver.Id;
            var primary = sectors.FirstOrDefault(s => IsTower(s.Type)) ?? sectors.FirstOrDefault();
            foreach (var s in sectors) { s.DocumentId = doc.Id; s.IsPrimary = s == primary; }
        }

        var b = new DocBuilder(_db, ver);
        var order = 0;

        // 1 — Regole piste (solo se presenti).
        if (airport.RunwayRules.Count > 0)
        {
            var sec = b.Section("Regole piste", BlockSection.Airport, ++order);
            b.Table(sec, BlockTier.Reduced, new
            {
                columns = new[] { "Condizione", "DEP", "ARR", "Note" },
                unified = false,
                rows = airport.RunwayRules.OrderBy(r => r.Order)
                    .Select(r => (object)new { cells = new[] { RuleCondition(r), Dash(r.DepRunways), Dash(r.ArrRunways), r.Note ?? "—" } })
                    .ToList(),
            });
        }

        // 2 — Quote di transizione (TA + tabella TL).
        var trans = b.Section("Quote di transizione", BlockSection.Airport, ++order);
        b.Prose(trans, BlockTier.Reduced, airport.TransitionAltitudeFt is int taFt
            ? $"**Transition Altitude:** {taFt} ft" : "**Transition Altitude:** _da inserire_");
        if (airport.TransitionLevels.Count > 0)
            b.Table(trans, BlockTier.Reduced, new
            {
                columns = new[] { "QNH (hPa)", "Transition Level" },
                unified = false,
                rows = airport.TransitionLevels.OrderBy(t => t.Order)
                    .Select(t => (object)new { cells = new[] { QnhRange(t.QnhFrom, t.QnhTo), t.Level } }).ToList(),
            });

        // 3 — Frequenze (settori propri + ATIS + link risolti).
        var freqRows = new List<object>();
        foreach (var s in sectors.Where(s => !string.IsNullOrWhiteSpace(s.DefaultFrequency)))
        {
            var cells = new[] { FreqName(s.Type, s.Callsign), s.Callsign, s.DefaultFrequency! };
            freqRows.Add(IsTower(s.Type)
                ? new { primary = true, star = true, cells } : (object)new { cells });
        }
        if (!string.IsNullOrWhiteSpace(airport.AtisFrequency))
            freqRows.Add(new { cells = new[] { "ATIS", $"{icao}_ATIS", airport.AtisFrequency! } });
        foreach (var l in links)
            freqRows.Add(new { cells = new[] { l.LabelOverride ?? l.SourceFrequency!.Label, l.SourceFrequency!.Callsign, l.SourceFrequency!.FrequencyMhz } });
        var freq = b.Section("Frequenze", BlockSection.Frequencies, ++order);
        b.Table(freq, BlockTier.Reduced, new { columns = new[] { "Nome", "Callsign", "Frequenza" }, unified = false, rows = freqRows });

        // 4 — Piste.
        var rwy = b.Section("Piste", BlockSection.Airport, ++order);
        b.Table(rwy, BlockTier.Extended, new
        {
            columns = new[] { "Pista", "TORA", "LDA", "APP procedures", "Patterns", "Circling" },
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

        // 5 — SID (tabella se presenti, altrimenti callout placeholder).
        var sid = b.Section("SID", BlockSection.Airport, ++order);
        if (airport.Sids.Count > 0)
            b.Table(sid, BlockTier.Extended, new
            {
                columns = new[] { "Pista", "FIX", "SID", "Transition", "Initial climb", "Type", "Cat.", "WTC", "Condition" },
                unified = false,
                rows = airport.Sids.OrderBy(s => s.Order).Select(s => (object)new
                {
                    cells = new[] { Dash(s.Runway), s.Fix, s.Name, Dash(s.Transition), Dash(s.InitialClimb),
                        Dash(s.Type), Dash(s.Cat), Dash(s.Wtc), Dash(s.Condition) }
                }).ToList(),
            });
        else
            b.Callout(sid, CalloutKind.Info, "SID non ancora inserite", BlockTier.Extended,
                "🔄 Nessuna SID inserita. Aggiungile dall'editor aeroporto (l'import dal sectorfile GitHub è un follow-up).");

        doc.LastUpdatedUtc = now;
        doc.LastUpdatedAiracCycle = cycle;
        await _db.SaveChangesAsync(ct);
        return doc.Id;
    }

    // ---- helper ----

    private async Task<int> AirportIdAsync(string icao, CancellationToken ct) =>
        await _db.Airports.Where(a => a.Icao == icao).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
        ?? throw NotFound(icao);

    private static InvalidOperationException NotFound(string icao) => new($"Aeroporto {icao} inesistente.");

    private static string Dash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();

    /// <summary>TWR e I_TWR (AFIS) sono entrambe "torri" ai fini di frequenza primaria/etichetta.</summary>
    private static bool IsTower(SectorType type) => type is SectorType.Twr or SectorType.ITwr;

    private static string FreqName(SectorType type, string callsign) => type switch
    {
        SectorType.Del => "Clearance Delivery (se attivo)",
        SectorType.Gnd => "Ground (se attivo)",
        SectorType.Twr => "Tower",
        SectorType.ITwr => "Tower (informazioni)",
        SectorType.App => "Approach",
        _ => callsign,
    };

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

    private static string RuleCondition(AirportRunwayRule r)
    {
        var parts = new List<string>();
        if (r.WindDirFrom is int df && r.WindDirTo is int dt) parts.Add($"vento {df:000}–{dt:000}°");
        else if (r.WindDirFrom is int df2) parts.Add($"vento ≥ {df2:000}°");
        else if (r.WindDirTo is int dt2) parts.Add($"vento ≤ {dt2:000}°");
        if (r.WindSpeedMin is int sm && r.WindSpeedMax is int sx) parts.Add($"{sm}–{sx} kt");
        else if (r.WindSpeedMin is int sm2) parts.Add($"≥ {sm2} kt");
        else if (r.WindSpeedMax is int sx2) parts.Add($"≤ {sx2} kt");
        if (r.Rain == true) parts.Add("pioggia");
        if (r.Snow == true) parts.Add("neve");
        if (r.TimeFromUtcMin is int tf && r.TimeToUtcMin is int tt) parts.Add($"{Hhmm(tf)}–{Hhmm(tt)}Z");
        else if (r.TimeFromUtcMin is int tf2) parts.Add($"da {Hhmm(tf2)}Z");
        else if (r.TimeToUtcMin is int tt2) parts.Add($"fino {Hhmm(tt2)}Z");
        if (DaysLabel(r.DaysOfWeekMask) is string dl) parts.Add(dl);
        if (r.DateParity == DateParity.Even) parts.Add("giorni pari");
        else if (r.DateParity == DateParity.Odd) parts.Add("giorni dispari");
        return parts.Count == 0 ? "Qualsiasi" : string.Join(", ", parts);
    }

    private static string Hhmm(int min) => $"{min / 60:00}:{min % 60:00}";

    private static readonly string[] DayNames = { "Lun", "Mar", "Mer", "Gio", "Ven", "Sab", "Dom" };

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

        public DocumentSection Section(string title, BlockSection kind, int order)
        {
            var s = new DocumentSection
            {
                DocumentVersion = _ver, ParentSection = null, Title = title, Order = order,
                Depth = 0, SectionKind = kind, RowVersion = Guid.NewGuid().ToByteArray(),
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
