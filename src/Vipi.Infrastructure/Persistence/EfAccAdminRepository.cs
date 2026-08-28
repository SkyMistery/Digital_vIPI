using Microsoft.EntityFrameworkCore;
using Vipi.Application.Abstractions;
using Vipi.Application.Aor;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// Implementazione EF di <see cref="IAccAdminRepository"/>. Import = upsert ACC + settori CTR dalla sorgente,
/// preservando IsHidden e il contenimento esistenti. Niente cancellazioni (gli ACC non più in sorgente
/// restano nel DB; l'admin li nasconde).
/// </summary>
public sealed class EfAccAdminRepository : IAccAdminRepository
{
    private readonly VipiDbContext _db;
    private readonly ICallsignRenameService _rinomine;

    /// <param name="rinomine">
    /// Il motore delle rinomine. Il default non è «spento» ma il motore sullo <b>stesso</b> contesto: una
    /// rinomina non applicata non è una funzione in meno, è un fantasma in archivio, e non dev'essere
    /// possibile costruire questo repository in un modo che la salti.
    /// </param>
    public EfAccAdminRepository(VipiDbContext db, ICallsignRenameService? rinomine = null)
    {
        _db = db;
        _rinomine = rinomine ?? new EfCallsignRenameService(db);
    }

    private const int FssUpperFt = 19000;   // limite superiore di default dei settori FSS (GND→19000)
    private static bool IsFss(string? position) =>
        string.Equals(position?.Trim(), "FSS", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<AccAdminRow>> ListAccsAsync(CancellationToken ct = default) =>
        await _db.Accs.AsNoTracking()
            .OrderBy(a => a.Code)
            .Select(a => new AccAdminRow(a.Id, a.Code, a.Name, a.IsMilitary, a.IsHidden,
                a.IsForeign, a.SpecialAreasEnabled,
                _db.SpecialAreaCenters.Count(c => c.CenterId == a.Code)))
            .ToListAsync(ct);

    public async Task<int> SetSpecialAreasEnabledAsync(int accId, bool enabled, CancellationToken ct = default)
    {
        var acc = await _db.Accs.FirstOrDefaultAsync(a => a.Id == accId, ct)
                  ?? throw new InvalidOperationException($"ACC id {accId} inesistente.");
        acc.SpecialAreasEnabled = enabled;
        await _db.SaveChangesAsync(ct);

        // Spegnere significa anche liberare l'archivio: senza questo le aree resterebbero lì per sempre, ferme e
        // selezionabili. Chi le condivide con un altro ente abilitato le conserva (si toglie solo il legame).
        return enabled ? 0 : (await PruneSpecialAreasNotInAsync(acc.Code, Array.Empty<string>(), ct)).Removed;
    }

    public async Task<IReadOnlyList<AccSectorRow>> ListSubcentersAsync(CancellationToken ct = default) =>
        await _db.AccSectors.AsNoTracking()
            .OrderBy(s => s.CenterId).ThenBy(s => s.ComposePosition)
            .Select(s => new AccSectorRow(s.Id, s.ComposePosition, s.CenterId, s.Position, s.MiddleIdentifier,
                s.Frequency, s.LowerLimit, s.UpperLimit, s.IsHidden, s.RegionMapPolygon != null,
                s.Acc!.IsHidden))
            .ToListAsync(ct);

    public async Task SetHiddenAsync(int accId, bool hidden, CancellationToken ct = default)
    {
        var acc = await _db.Accs.FirstOrDefaultAsync(a => a.Id == accId, ct)
                  ?? throw new InvalidOperationException($"ACC id {accId} inesistente.");
        acc.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetSubcenterHiddenAsync(int id, bool hidden, CancellationToken ct = default)
    {
        var s = await _db.AccSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException(Lingua($"Settore ATC id {id} inesistente.", $"ATC sector id {id} does not exist."));
        s.IsHidden = hidden;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<SubcenterHideContext?> GetSubcenterHideContextAsync(int id, CancellationToken ct = default)
    {
        var s = await _db.AccSectors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null) return null;
        var cs = s.ComposePosition;

        // ACC nascosti: i loro settori NON contano come figli visibili.
        var hidden = (await _db.Accs.Where(a => a.IsHidden).Select(a => a.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Figli (cross-catalogo) non nascosti che nominano questo settore come padre.
        var accChildCenters = await _db.AccSectors.AsNoTracking()
            .Where(x => x.ParentCallsign == cs && !x.IsHidden).Select(x => x.CenterId).ToListAsync(ct);
        var airChildCenters = await _db.AirportSectors.AsNoTracking()
            .Where(x => x.ParentCallsign == cs && !x.IsHidden).Select(x => x.AccCode).ToListAsync(ct);
        var hasVisibleChildren = accChildCenters.Concat(airChildCenters).Any(code => !hidden.Contains(code));

        return new SubcenterHideContext(cs, s.ParentCallsign, s.CenterId, hasVisibleChildren);
    }

    public async Task SetSubcenterLimitsAsync(int id, int? lower, int? upper, CancellationToken ct = default)
    {
        var s = await _db.AccSectors.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new InvalidOperationException(Lingua($"Settore ATC id {id} inesistente.", $"ATC sector id {id} does not exist."));
        s.LowerLimit = lower ?? 0;     // inferiore: vuoto → 0
        s.UpperLimit = upper;          // superiore: vuoto → null = UNL
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Upsert delle aree. ⚠️ Oltre a creare e aggiornare, dice <b>che cosa è cambiato davvero</b>: fino al
    /// 25 agosto 2026 il contatore <c>updated</c> saliva per ogni riga toccata, senza confrontare niente —
    /// «aggiornata» ogni notte per tutte. Chi lo usasse per segnalare qualcosa segnalerebbe il nulla, in
    /// continuazione. Il confronto guarda solo i campi che un documento <b>mostra</b>.
    /// </summary>
    public async Task<SpecialAreaUpsertOutcome> ImportSpecialAreasAsync(IReadOnlyList<SourceSpecialArea> areas, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        int created = 0, updated = 0;
        var cambiate = new List<SpecialAreaRef>();

        var accCodes = (await _db.Accs.Select(a => a.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await _db.SpecialAreas
            .ToDictionaryAsync(s => s.IvaoId, StringComparer.OrdinalIgnoreCase, ct);
        var links = (await _db.SpecialAreaCenters.ToListAsync(ct))
            .ToDictionary(l => LinkKey(l.IvaoId, l.CenterId), StringComparer.OrdinalIgnoreCase);
        var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // dedup dentro il batch di QUESTO ACC

        foreach (var a in areas)
        {
            var ivaoId = (a.IvaoId ?? "").Trim();
            if (ivaoId.Length == 0) continue;
            if (!handled.Add(ivaoId)) continue;         // già trattata in questo batch → salta
            var center = a.CenterId.Trim().ToUpperInvariant();
            if (!accCodes.Contains(center)) continue;   // niente ACC corrispondente → salta (FK)

            // Legame area↔ACC: additivo. Un altro centro che elenca la stessa area non se la porta via (era il
            // difetto del vecchio CenterId singolo: vinceva l'ultimo ACC in ordine alfabetico).
            if (links.TryGetValue(LinkKey(ivaoId, center), out var link)) link.ImportedAtUtc = now;
            else
            {
                link = new SpecialAreaCenter { IvaoId = ivaoId, CenterId = center, ImportedAtUtc = now };
                _db.SpecialAreaCenters.Add(link);
                links[LinkKey(ivaoId, center)] = link;
            }

            if (existing.TryGetValue(ivaoId, out var row))
            {
                // La fotografia di PRIMA, sui soli campi che finiscono sotto gli occhi di un lettore: nome,
                // tipo, quote, raggio e testi dell'attivazione. La shape no — un poligono che si sposta di
                // qualche metro non è una cosa che il documento «dice».
                var prima = (row.Type, row.Name, row.Description, row.ActivationDetails,
                             row.MinimumAlt, row.MaximumAlt, row.Range);
                row.Type = a.Type;
                row.Name = a.Name;
                row.Description = a.Description;
                row.ActivationDetails = a.ActivationDetails;
                row.MinimumAlt = a.MinimumAlt;
                row.MaximumAlt = a.MaximumAlt;
                row.Range = a.Range;
                // Preserva la shape quando il dettaglio manca (null) O quando arriva vuota. ⚠️ La seconda metà
                // è nuova, e senza di lei le 228 aree in archivio si salvavano solo per fortuna: le protegge
                // `skipDetailIds`, che per un'area con shape già buona il dettaglio non lo chiede nemmeno —
                // ma basterebbe un'area nuova, o un giro che riscarica tutto, per riportarci `[]` e perderla.
                if (!PolygonGeometry.IsEmptyShape(a.RegionMapPolygon)) row.RegionMapPolygon = a.RegionMapPolygon;
                row.ImportedAtUtc = now;
                updated++;
                if (prima != (row.Type, row.Name, row.Description, row.ActivationDetails,
                              row.MinimumAlt, row.MaximumAlt, row.Range))
                    cambiate.Add(new SpecialAreaRef(ivaoId, row.Name ?? ivaoId));
            }
            else
            {
                _db.SpecialAreas.Add(new SpecialArea
                {
                    IvaoId = ivaoId,
                    Type = a.Type,
                    Name = a.Name,
                    Description = a.Description,
                    ActivationDetails = a.ActivationDetails,
                    MinimumAlt = a.MinimumAlt,
                    MaximumAlt = a.MaximumAlt,
                    Range = a.Range,
                    RegionMapPolygon = PolygonGeometry.IsEmptyShape(a.RegionMapPolygon) ? null : a.RegionMapPolygon,
                    ImportedAtUtc = now,
                });
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new SpecialAreaUpsertOutcome(created, updated, cambiate);
    }

    public async Task<IReadOnlySet<string>> ListAreasWithFreshShapeAsync(string accCode, DateTime importedAfterUtc, CancellationToken ct = default)
    {
        accCode = accCode.Trim().ToUpperInvariant();
        var ids = await _db.SpecialAreas.AsNoTracking()
            .Where(s => s.Centers.Any(c => c.CenterId == accCode) && s.RegionMapPolygon != null
                        && s.ImportedAtUtc != null && s.ImportedAtUtc > importedAfterUtc)
            .Select(s => s.IvaoId)
            .ToListAsync(ct);
        return ids.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<SpecialAreaPruneOutcome> PruneSpecialAreasNotInAsync(string accCode, IReadOnlyCollection<string> keepIvaoIds, CancellationToken ct = default)
    {
        accCode = accCode.Trim().ToUpperInvariant();
        var keep = keepIvaoIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Si potano i LEGAMI di questo ACC, non le aree: «LIRR non la elenca più» non vuol dire che sia sparita,
        // può restare del militare. L'area si cancella solo quando resta senza nessun ente che la elenchi.
        var links = await _db.SpecialAreaCenters.Where(l => l.CenterId == accCode).ToListAsync(ct);
        var remove = links.Where(l => !keep.Contains(l.IvaoId)).ToList();
        if (remove.Count == 0) return SpecialAreaPruneOutcome.Empty;

        // Le due rimozioni — legami e aree rimaste orfane — stanno in UN SOLO SaveChanges, quindi in una
        // sola transazione implicita. Prima erano due: fra l'una e l'altra un guasto (rete, riavvio del
        // server) lasciava i legami cancellati e le aree orfane in archivio, cioè righe che nessun ente
        // elenca più e che nessuna passata successiva sarebbe tornata a guardare.
        //
        // Per farlo, le orfane si calcolano PRIMA di cancellare: la vecchia versione chiedeva al database
        // «quali aree non hanno più legami», domanda che ha senso solo dopo che la cancellazione è stata
        // scritta — ed era la ragione per cui i SaveChanges dovevano essere due. Qui si guarda invece se
        // resta qualche legame di un ALTRO ente, che è la stessa cosa senza dover scrivere prima: la chiave
        // primaria di SpecialAreaCenter è (IvaoId, CenterId), quindi per questo ACC il legame è al più uno,
        // ed è fra quelli che stiamo togliendo.
        var idsToccati = remove.Select(l => l.IvaoId).ToList();
        var conAltriEnti = await _db.SpecialAreaCenters
            .Where(l => idsToccati.Contains(l.IvaoId) && l.CenterId != accCode)
            .Select(l => l.IvaoId)
            .Distinct()
            .ToListAsync(ct);

        var orfane = await _db.SpecialAreas
            .Where(a => idsToccati.Contains(a.IvaoId) && !conAltriEnti.Contains(a.IvaoId))
            .ToListAsync(ct);

        // I nomi PRIMA di cancellare: dopo, di quelle righe non resta niente da leggere — ed è il nome, non
        // l'id numerico di IVAO, quello che un editore riconosce nel proprio documento.
        var nomi = await _db.SpecialAreas.AsNoTracking()
            .Where(a => idsToccati.Contains(a.IvaoId))
            .Select(a => new SpecialAreaRef(a.IvaoId, a.Name ?? a.IvaoId))
            .ToListAsync(ct);

        _db.SpecialAreaCenters.RemoveRange(remove);
        if (orfane.Count > 0) _db.SpecialAreas.RemoveRange(orfane);
        await _db.SaveChangesAsync(ct);

        return new SpecialAreaPruneOutcome(remove.Count, nomi);
    }

    // Chiave del legame area↔ACC in memoria (l'id IVAO è numerico, il codice ACC già normalizzato maiuscolo).
    private static string LinkKey(string ivaoId, string centerId) => ivaoId + "|" + centerId;

    public async Task<(int Created, int Updated)> ImportSubcentersAsync(IReadOnlyList<SourceSubcenter> subs, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        int created = 0, updated = 0;

        // PRIMA di tutto: le rinomine, riconosciute per identità. Applicate qui — cioè prima che si legga
        // `existing` — l'upsert per callsign qui sotto ritrova le righe al loro posto e non ha bisogno di
        // sapere che qualcosa è successo. È il motivo per cui questo blocco sta in cima e non in fondo.
        await _rinomine.ApplyAsync(
            CallsignRenameDetector.Detect(
                SourceCatalog.Subcenter,
                await _db.AccSectors.AsNoTracking().Where(x => x.IvaoId != null)
                    .ToDictionaryAsync(x => x.IvaoId!.Value, x => x.ComposePosition, ct),
                subs.Select(s => (s.IvaoId, s.ComposePosition))),
            ct);

        var accCodes = (await _db.Accs.Select(a => a.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await _db.AccSectors
            .ToDictionaryAsync(s => s.ComposePosition, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var s in subs)
        {
            var compose = s.ComposePosition.Trim().ToUpperInvariant();
            if (compose.Length == 0) continue;
            var center = s.CenterId.Trim().ToUpperInvariant();
            if (!accCodes.Contains(center)) continue;   // niente ACC corrispondente → salta (FK)

            if (existing.TryGetValue(compose, out var row))
            {
                row.IvaoId ??= s.IvaoId;   // backfill: la riga c'era prima che l'identità esistesse
                row.CenterId = center;
                row.Position = s.Position;
                row.MiddleIdentifier = s.MiddleIdentifier;
                row.AtcCallsign = s.AtcCallsign;
                row.Frequency = s.Frequency;
                // Solo una shape VERA sovrascrive: l'assenza non è un ordine di cancellare (PolygonGeometry.IsEmptyShape).
                if (!PolygonGeometry.IsEmptyShape(s.RegionMapPolygon))
                {
                    row.RegionMapPolygon = s.RegionMapPolygon;
                    // ⚠️ E l'anagrafica riprende il comando per intero: provenienza e differimento tornano a
                    // zero. Vedi il gemello in EfAirportSectorRepository per il perché — è la riga che
                    // scatterà quando IVAO sistemerà il guasto dei poligoni.
                    row.ShapeSource = ShapeSource.Source;
                    row.RegionMapPolygonInForce = null;
                    row.ShapeAiracCycle = null;
                    row.ShapeForcePublished = false;
                }
                // Limiti: l'admin comanda; aggiorna solo se la sorgente li espone (oggi null → preserva).
                if (s.LowerLimit is not null) row.LowerLimit = s.LowerLimit;
                else row.LowerLimit ??= 0;                 // default inferiore = GND (0)
                if (s.UpperLimit is not null) row.UpperLimit = s.UpperLimit;
                else if (row.UpperLimit is null && IsFss(s.Position)) row.UpperLimit = FssUpperFt;  // FSS: GND→19000
                // (altri) superiore null = UNL (illimitato)
                row.ImportedAtUtc = now;
                updated++;
            }
            else
            {
                _db.AccSectors.Add(new AccSector
                {
                    IvaoId = s.IvaoId,
                    ComposePosition = compose,
                    CenterId = center,
                    Position = s.Position,
                    MiddleIdentifier = s.MiddleIdentifier,
                    AtcCallsign = s.AtcCallsign,
                    Frequency = s.Frequency,
                    RegionMapPolygon = PolygonGeometry.IsEmptyShape(s.RegionMapPolygon) ? null : s.RegionMapPolygon,
                    LowerLimit = s.LowerLimit ?? 0,        // default GND (0)
                    UpperLimit = s.UpperLimit ?? (IsFss(s.Position) ? FssUpperFt : (int?)null),  // FSS→19000, altri→UNL
                    IsHidden = false,
                    ImportedAtUtc = now,
                });
                created++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (created, updated);
    }

    public async Task<(int Created, int Updated)> ImportAsync(IReadOnlyList<SourceCenter> centers, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        int accsCreated = 0, accsUpdated = 0;

        // Ogni center area della sorgente = un ACC. Upsert per codice (centerId). Niente settori.
        var groups = centers
            .GroupBy(c => c.CenterId, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key))
            .ToList();

        var existingAccs = await _db.Accs.ToDictionaryAsync(a => a.Code, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var g in groups)
        {
            var code = g.Key.Trim().ToUpperInvariant();
            var name = g.Select(c => c.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? code;
            var military = g.Any(c => c.Military);

            if (existingAccs.TryGetValue(code, out var acc))
            {
                acc.Name = name;
                acc.IsMilitary = military;
                acc.ImportedAtUtc = now;
                accsUpdated++;
            }
            else
            {
                _db.Accs.Add(new Acc
                {
                    Code = code,
                    Name = name,
                    CountryPrefix = code.Length >= 2 ? code[..2] : code,
                    IsMilitary = military,
                    IsHidden = false,
                    ImportedAtUtc = now,
                });
                accsCreated++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return (accsCreated, accsUpdated);
    }
}
