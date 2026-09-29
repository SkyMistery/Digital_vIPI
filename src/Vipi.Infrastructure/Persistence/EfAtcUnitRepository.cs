using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain.Entities;
using Vipi.Domain.Services;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>EF degli enti ATC (vedi <see cref="AtcUnit"/>).</summary>
public sealed class EfAtcUnitRepository : IAtcUnitRepository
{
    private readonly VipiDbContext _db;
    private readonly IAiracService _airac;

    public EfAtcUnitRepository(VipiDbContext db, IAiracService? airac = null)
    {
        _db = db;
        _airac = airac ?? new AiracService();
    }

    private static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();

    private IQueryable<AtcUnit> Con() =>
        _db.AtcUnits.AsNoTracking().Include(u => u.Positions).Include(u => u.Acc);

    internal static AtcUnitRow Riga(AtcUnit u) => new(u.Id, u.Code, u.Name, u.Acc?.Code ?? "", u.Mode, u.DocumentId,
        u.Positions.OrderBy(p => p.Order).ThenBy(p => p.Id).Select(p => p.Callsign).ToList(), u.GroupKey);

    public async Task<AtcUnitRow?> FindAsync(string key, CancellationToken ct = default)
    {
        var k = Norm(key);
        if (k.Length == 0) return null;
        // Il codice vince sulla posizione: un codice è per sempre, una posizione può passare a un altro ente.
        var u = await Con().FirstOrDefaultAsync(x => x.Code == k, ct)
                ?? await Con().FirstOrDefaultAsync(x => x.Positions.Any(p => p.Callsign == k), ct);
        return u is null ? null : Riga(u);
    }

    public async Task<IReadOnlySet<string>> ActiveCallsignsAsync(IReadOnlyCollection<string> callsigns, CancellationToken ct = default)
    {
        var cercati = callsigns.Select(Norm).Where(c => c.Length > 0).Distinct().ToList();
        if (cercati.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // ⚠️ Il confronto fra maiuscole e minuscole nel HashSet, non nella query: i provider hanno collation diverse.
        return (await _db.Sectors.AsNoTracking().Where(s => s.IsActive && cercati.Contains(s.Callsign))
                .Select(s => s.Callsign).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Un gruppo APP come lo dice la vIPI ACC: chiave, titolo, membri e l'ente da cui è nato (S50).</summary>
    private sealed record GruppoAcc(string Key, string Title, List<string> Membri, int? UnitId);

    public async Task<int> AllineaGruppiAccAsync(string? accCode = null, CancellationToken ct = default)
    {
        // La vIPI ACC: il documento del CTR radice di ogni ACC (come ResolveAccDocumentIdentityAsync).
        var radici = _db.Sectors.AsNoTracking()
            .Where(s => s.Type == Domain.SectorType.Ctr && s.ParentSectorId == null && s.IsActive && s.DocumentId != null);
        if (!string.IsNullOrWhiteSpace(accCode))
        {
            var acc = Norm(accCode);
            radici = radici.Where(s => s.Acc!.Code == acc);
        }
        var documenti = (await radici.Select(s => new { s.AccId, AccCode = s.Acc!.Code, DocumentId = s.DocumentId!.Value })
                .ToListAsync(ct))
            .GroupBy(d => d.AccId).Select(g => g.First()).ToList();

        var nati = 0;
        foreach (var d in documenti)
        {
            var gruppi = await GruppiDiAsync(d.DocumentId, ct);
            if (gruppi.Count == 0) continue;
            nati += await AllineaAsync(d.AccId, d.AccCode, gruppi, ct);
        }
        return nati;
    }

    /// <summary>I gruppi APP della bozza e della versione pubblicata del documento; per la stessa chiave vince la bozza.</summary>
    private async Task<List<GruppoAcc>> GruppiDiAsync(int documentId, CancellationToken ct)
    {
        var versioni = await _db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == documentId
                        && (v.Status == Domain.DocumentStatus.Draft || v.Status == Domain.DocumentStatus.Published))
            .Select(v => new { v.Id, v.Status, v.VersionNumber })
            .ToListAsync(ct);
        var perChiave = new Dictionary<string, GruppoAcc>(StringComparer.OrdinalIgnoreCase);
        // Prima la pubblicata, poi la bozza (più recente per ultima): chi arriva dopo sovrascrive.
        foreach (var v in versioni.OrderBy(v => v.Status == Domain.DocumentStatus.Draft ? 1 : 0).ThenBy(v => v.VersionNumber))
        {
            var sezioni = await _db.DocumentSections.AsNoTracking()
                .Where(s => s.DocumentVersionId == v.Id && s.ParentSectionId == null && s.SectionKey == "appgroup")
                .Select(s => new { s.Id, s.Title })
                .ToListAsync(ct);
            foreach (var s in sezioni)
            {
                var corpi = await _db.ContentBlocks.AsNoTracking()
                    .Where(b => b.SectionId == s.Id && b.BodyJson != null)
                    .OrderBy(b => b.Order).Select(b => b.BodyJson!).ToListAsync(ct);
                var meta = corpi.Select(Meta).FirstOrDefault(m => m is { Kind: AccBlockKind.AppGroup } && !string.IsNullOrWhiteSpace(m.Key));
                if (meta is null) continue;
                perChiave[meta.Key] = new GruppoAcc(meta.Key, s.Title,
                    meta.MemberCallsigns.Select(Norm).Where(c => c.Length > 0).Distinct().ToList(), meta.UnitId);
            }
        }
        return perChiave.Values.ToList();

        static AccBlockMeta? Meta(string json)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<AccBlockMeta>(json); }
            catch (System.Text.Json.JsonException) { return null; }
        }
    }

    private async Task<int> AllineaAsync(int accId, string accCode, List<GruppoAcc> gruppi, CancellationToken ct)
    {
        var enti = await _db.AtcUnits.Include(u => u.Positions).ToListAsync(ct);
        var nati = 0;
        foreach (var g in gruppi)
        {
            var ente = enti.FirstOrDefault(u => u.AccId == accId && string.Equals(u.GroupKey, g.Key, StringComparison.OrdinalIgnoreCase))
                       ?? (g.UnitId is int id ? enti.FirstOrDefault(u => u.Id == id) : null);

            // Le posizioni che il gruppo può prendere: non di un altro ente, e non il codice di un altro ente.
            List<string> Libere(AtcUnit? io) => g.Membri
                .Where(m => !enti.Any(u => u != io && (u.Positions.Any(p => p.Callsign == m) || u.Code == m)))
                .ToList();

            if (ente is null)
            {
                var libere = Libere(null);
                // Un gruppo appena aggiunto nell'editor non ha ancora membri: l'ente nasce quando ne avrà.
                if (libere.Count == 0) continue;
                var codice = libere.FirstOrDefault(m => enti.All(u => u.Code != m))
                             ?? $"{accCode}_{g.Key.Replace("grp:", "", StringComparison.OrdinalIgnoreCase)}".ToUpperInvariant();
                ente = new AtcUnit
                {
                    Code = codice, Name = string.IsNullOrWhiteSpace(g.Title) ? codice : g.Title.Trim(), AccId = accId,
                    Mode = Domain.AtcUnitMode.InAccVipi, GroupKey = g.Key,
                };
                for (var i = 0; i < libere.Count; i++) ente.Positions.Add(new AtcUnitPosition { Callsign = libere[i], Order = i });
                _db.AtcUnits.Add(ente);
                enti.Add(ente);
                nati++;
                continue;
            }

            ente.GroupKey ??= g.Key;
            // ⚠️ Un ente che ha ancora la sua vIPI APP (spostamento in corso, S52) non si tocca: le sue posizioni le
            // decide il riquadro «Ente», e il gruppo è solo una copia in bozza.
            if (ente.Mode != Domain.AtcUnitMode.InAccVipi) continue;
            if (!string.IsNullOrWhiteSpace(g.Title)) ente.Name = g.Title.Trim();
            var voluti = Libere(ente);
            foreach (var p in ente.Positions.Where(p => !voluti.Contains(p.Callsign)).ToList())
            {
                ente.Positions.Remove(p);
                _db.AtcUnitPositions.Remove(p);
            }
            for (var i = 0; i < voluti.Count; i++)
            {
                var p = ente.Positions.FirstOrDefault(x => x.Callsign == voluti[i]);
                if (p is null) ente.Positions.Add(new AtcUnitPosition { Callsign = voluti[i], Order = i });
                else p.Order = i;
            }
        }
        await _db.SaveChangesAsync(ct);
        return nati;
    }

    public async Task<AtcUnitRow?> GetAsync(int unitId, CancellationToken ct = default) =>
        await Con().FirstOrDefaultAsync(u => u.Id == unitId, ct) is { } u ? Riga(u) : null;

    public async Task<IReadOnlyList<AtcUnitRow>> ListAsync(string? accCode = null, CancellationToken ct = default)
    {
        var q = Con();
        if (!string.IsNullOrWhiteSpace(accCode))
        {
            var acc = Norm(accCode);
            q = q.Where(u => u.Acc!.Code == acc);
        }
        return (await q.OrderBy(u => u.Code).ToListAsync(ct)).Select(Riga).ToList();
    }

    public async Task<int> EnsureDocumentAsync(string code, string name, string accCode, SectionProfile profile,
        int authorUserId, CancellationToken ct = default)
    {
        code = Norm(code);
        var unit = await _db.AtcUnits.Include(u => u.Positions).FirstOrDefaultAsync(u => u.Code == code, ct);
        if (unit?.DocumentId is int esistente) return esistente;   // idempotente

        if (unit is null)
        {
            var acc = Norm(accCode);
            var accId = await _db.Accs.Where(a => a.Code == acc).Select(a => (int?)a.Id).FirstOrDefaultAsync(ct)
                        ?? throw new InvalidOperationException(Lingua($"ACC {acc} inesistente.", $"ACC {acc} does not exist."));
            unit = new AtcUnit { Code = code, Name = string.IsNullOrWhiteSpace(name) ? code : name.Trim(), AccId = accId };
            // Nasce con la posizione da cui nasce, se nessun altro ente la tiene già.
            if (!await _db.AtcUnitPositions.AnyAsync(p => p.Callsign == code, ct))
                unit.Positions.Add(new AtcUnitPosition { Callsign = code, Order = 0 });
            _db.AtcUnits.Add(unit);
        }

        // La nascita è condivisa con le altre famiglie (Seed/DocumentBirth): documento, prima versione bozza e
        // le sezioni del profilo, coi segnaposto sulle sezioni rese dalla pagina.
        var (doc, _) = Seed.DocumentBirth.Crea(_db, _airac, unit.Name, Domain.Language.It, profile, authorUserId);
        await _db.SaveChangesAsync(ct);
        unit.DocumentId = doc.Id;
        await _db.SaveChangesAsync(ct);
        return doc.Id;
    }

    private async Task<AtcUnit> CaricaAsync(int unitId, CancellationToken ct) =>
        await _db.AtcUnits.Include(u => u.Positions).FirstOrDefaultAsync(u => u.Id == unitId, ct)
        ?? throw new ValidationException(Lingua($"Ente {unitId} inesistente.", $"Unit {unitId} does not exist."));

    public async Task AddPositionAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        var cs = Norm(callsign);
        if (cs.Length == 0) throw new ValidationException(Lingua("Nominativo vuoto.", "Empty callsign."));
        var unit = await CaricaAsync(unitId, ct);
        if (unit.Positions.Any(p => p.Callsign == cs)) return;
        var altro = await _db.AtcUnitPositions.Where(p => p.Callsign == cs).Select(p => p.AtcUnit!.Name).FirstOrDefaultAsync(ct);
        if (altro is not null)
            throw new ValidationException(Lingua($"{cs} è già una posizione di «{altro}».", $"{cs} is already a position of «{altro}»."));
        // ⚠️ Nemmeno il CODICE di un altro ente (revisione, S52): nella ricerca il codice vince sulla posizione, e
        // la posizione nuova non porterebbe mai al suo ente — indirizzo, vista live ed editor aprirebbero l'altro.
        var diCodice = await _db.AtcUnits.Where(u => u.Code == cs && u.Id != unitId).Select(u => u.Name).FirstOrDefaultAsync(ct);
        if (diCodice is not null)
            throw new ValidationException(Lingua(
                $"{cs} è il codice di «{diCodice}»: la vIPI di quell'ente si pubblica con questo nome.",
                $"{cs} is the code of «{diCodice}»: that unit's vIPI is published under this name."));
        unit.Positions.Add(new AtcUnitPosition
        {
            Callsign = cs, Order = unit.Positions.Count == 0 ? 0 : unit.Positions.Max(p => p.Order) + 1,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemovePositionAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        var cs = Norm(callsign);
        var unit = await CaricaAsync(unitId, ct);
        var p = unit.Positions.FirstOrDefault(x => x.Callsign == cs);
        if (p is null) return;
        _db.AtcUnitPositions.Remove(p);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MakePrimaryAsync(int unitId, string callsign, CancellationToken ct = default)
    {
        var cs = Norm(callsign);
        var unit = await CaricaAsync(unitId, ct);
        if (unit.Positions.All(p => p.Callsign != cs)) return;
        var ordine = 1;
        foreach (var p in unit.Positions.OrderBy(p => p.Order).ThenBy(p => p.Id))
            p.Order = p.Callsign == cs ? 0 : ordine++;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RenameAsync(int unitId, string name, CancellationToken ct = default)
    {
        var n = (name ?? "").Trim();
        if (n.Length == 0) throw new ValidationException(Lingua("Il nome dell'ente è vuoto.", "The unit name is empty."));
        var unit = await CaricaAsync(unitId, ct);
        unit.Name = n;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetModeAsync(int unitId, Domain.AtcUnitMode mode, CancellationToken ct = default)
    {
        var unit = await CaricaAsync(unitId, ct);
        unit.Mode = mode;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RenamePositionAsync(string oldCallsign, string newCallsign, CancellationToken ct = default)
    {
        var vecchio = Norm(oldCallsign);
        var nuovo = Norm(newCallsign);
        var p = await _db.AtcUnitPositions.FirstOrDefaultAsync(x => x.Callsign == vecchio, ct);
        if (p is null || vecchio == nuovo) return;
        if (await _db.AtcUnitPositions.AnyAsync(x => x.Callsign == nuovo, ct)) { _db.AtcUnitPositions.Remove(p); }
        else p.Callsign = nuovo;
        await _db.SaveChangesAsync(ct);
    }
}
