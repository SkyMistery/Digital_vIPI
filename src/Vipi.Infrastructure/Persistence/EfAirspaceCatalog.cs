using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Domain;
using Vipi.Domain.Entities;
using static Vipi.Application.Messaggio;

namespace Vipi.Infrastructure.Persistence;

/// <summary>
/// EF: il catalogo degli spazi aerei dell'AIP (carta del 29 agosto 2026). Le regole stanno scritte su
/// <see cref="IAirspaceCatalog"/>; qui c'è come si applicano.
///
/// <para>⚠️ <b>Il file si conserva intero</b>, e il salvataggio è <b>tutto o niente</b>: un caricamento a
/// metà — l'intestazione senza i volumi — sarebbe un catalogo che dichiara 1 536 volumi e ne ha 300, e
/// nessuna pagina saprebbe dirlo.</para>
/// </summary>
public sealed class EfAirspaceCatalog : IAirspaceCatalog
{
    private readonly VipiDbContext _db;

    public EfAirspaceCatalog(VipiDbContext db) => _db = db;

    public async Task<IReadOnlyList<AirspaceImportRow>> ListImportsAsync(CancellationToken ct = default) =>
        (await _db.AirspaceImports.AsNoTracking()
            .OrderByDescending(i => i.UploadedUtc).ThenByDescending(i => i.Id)
            .ToListAsync(ct))
        .Select(Riga).ToList();

    public async Task<AirspaceImportRow?> GetCurrentAsync(CancellationToken ct = default)
    {
        var corrente = await _db.AirspaceImports.AsNoTracking().FirstOrDefaultAsync(i => i.IsCurrent, ct);
        return corrente is null ? null : Riga(corrente);
    }

    public async Task<AirspaceImportRow> SaveAsync(
        NewAirspaceImport header, AirspaceReadResult read, DateTime nowUtc, CancellationToken ct = default)
    {
        var caricamento = new AirspaceImport
        {
            FileName = Taglia(header.FileName, 260) ?? "spazi-aerei.kmz",
            Sha256 = Convert.ToHexString(SHA256.HashData(header.Content)).ToLowerInvariant(),
            Content = header.Content,
            SizeBytes = header.Content.LongLength,
            AiracCycle = string.IsNullOrWhiteSpace(header.AiracCycle) ? null : header.AiracCycle.Trim(),
            GeneratedUtc = read.GeneratedUtc,
            UploadedUtc = nowUtc,
            UploadedByUserId = header.UserId,
            UploadedByName = Taglia(header.UserName, 128),
            VolumesRead = read.Volumes.Count,
            VolumesUsable = read.Volumes.Count(v => v.IsUsable),
            DuplicateKeys = read.Issues.Count(i => i.Kind == AirspaceIssueKind.ChiaveDuplicata),
            PointCount = read.Volumes.Sum(v => v.PointCount),
            IssuesJson = JsonSerializer.Serialize(read.Issues),
            IsCurrent = true,
        };

        foreach (var v in read.Volumes)
        {
            // ⚠️ Si archivia il PRIMO anello, e `RingCount` dice quanti ne aveva: sul file vero sono uno su
            // tutti e 1 536, e il giorno che non lo saranno la pagina lo dice invece di perdere in silenzio
            // metà di un confine.
            var forma = AirspaceShapeBuilder.Build(v.Rings.FirstOrDefault());
            if (forma is null) continue;   // il lettore l'ha già segnalato: qui non si inventa un poligono

            caricamento.Volumes.Add(new AirspaceVolume
            {
                NaturalKey = v.NaturalKey,
                Ordinal = v.Ordinal,
                Family = v.Family,
                Name = v.Name,
                Category = v.Category,
                AirspaceClass = v.AirspaceClass,
                BaseDatum = v.Base.Datum,
                BaseFeet = v.Base.Feet,
                BaseRaw = Taglia(v.Base.Raw, 32)!,
                TopDatum = v.Top.Datum,
                TopFeet = v.Top.Feet,
                TopRaw = Taglia(v.Top.Raw, 32)!,
                PolygonJson = forma.PolygonJson,
                RingCount = v.Rings.Count,
                PointCount = forma.PointCount,
                MinLat = forma.MinLat,
                MinLon = forma.MinLon,
                MaxLat = forma.MaxLat,
                MaxLon = forma.MaxLon,
            });
        }

        // Il nuovo entra in vigore e spegne il precedente: «in vigore» è uno solo, ed è la domanda a cui
        // tutte le altre pagine rispondono senza chiedere quale.
        await _db.AirspaceImports.Where(i => i.IsCurrent).ExecuteUpdateAsync(
            s => s.SetProperty(i => i.IsCurrent, false), ct);

        _db.AirspaceImports.Add(caricamento);
        await _db.SaveChangesAsync(ct);
        return Riga(caricamento);
    }

    public async Task<IReadOnlyList<AirspaceVolumeRow>> ListVolumesAsync(
        AirspaceVolumeQuery query, CancellationToken ct = default)
    {
        var importId = query.ImportId ?? await CurrentIdAsync(ct);
        if (importId is null) return Array.Empty<AirspaceVolumeRow>();

        var q = _db.AirspaceVolumes.AsNoTracking().Where(v => v.ImportId == importId);

        if (query.Families is { Count: > 0 } famiglie)
            q = q.Where(v => famiglie.Contains(v.Family));
        else if (query.UsableOnly)
            q = q.Where(v => AirspaceFamilies.Usable.Contains(v.Family));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var testo = query.Search.Trim();
            q = q.Where(v => EF.Functions.Like(v.Name, $"%{testo}%"));
        }

        var righe = await q.OrderBy(v => v.Family).ThenBy(v => v.Name).ThenBy(v => v.Ordinal)
            .Take(Math.Clamp(query.Take, 1, 5000)).ToListAsync(ct);
        return righe.Select(Riga).ToList();
    }

    public async Task<IReadOnlyList<AirspaceVolumeRow>> GetVolumesAsync(
        IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return Array.Empty<AirspaceVolumeRow>();

        var righe = await _db.AirspaceVolumes.AsNoTracking().Where(v => ids.Contains(v.Id)).ToListAsync(ct);
        var indice = righe.ToDictionary(v => v.Id);

        var esito = new List<AirspaceVolumeRow>(ids.Count);
        foreach (var id in ids)
            if (indice.TryGetValue(id, out var v))
                esito.Add(Riga(v));
        return esito;   // ⚠️ NELL'ORDINE CHIESTO: è l'ordine in cui i volumi sono stati agganciati.
    }

    public async Task<IReadOnlyDictionary<AirspaceFamily, int>> CountByFamilyAsync(
        int? importId = null, CancellationToken ct = default)
    {
        var id = importId ?? await CurrentIdAsync(ct);
        if (id is null) return new Dictionary<AirspaceFamily, int>();

        return await _db.AirspaceVolumes.AsNoTracking().Where(v => v.ImportId == id)
            .GroupBy(v => v.Family).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N, ct);
    }

    public async Task<IReadOnlyList<AirspaceIssue>> GetIssuesAsync(int importId, CancellationToken ct = default)
    {
        var json = await _db.AirspaceImports.AsNoTracking()
            .Where(i => i.Id == importId).Select(i => i.IssuesJson).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<AirspaceIssue>();

        try
        {
            return JsonSerializer.Deserialize<List<AirspaceIssue>>(json) ?? new List<AirspaceIssue>();
        }
        catch (JsonException)
        {
            return Array.Empty<AirspaceIssue>();   // una diagnostica illeggibile non fa cadere la pagina
        }
    }

    public async Task<(string FileName, byte[] Content)?> GetFileAsync(int importId, CancellationToken ct = default)
    {
        var riga = await _db.AirspaceImports.AsNoTracking()
            .Where(i => i.Id == importId).Select(i => new { i.FileName, i.Content }).FirstOrDefaultAsync(ct);
        return riga is null ? null : (riga.FileName, riga.Content);
    }

    public async Task SetCurrentAsync(int importId, CancellationToken ct = default)
    {
        if (!await _db.AirspaceImports.AnyAsync(i => i.Id == importId, ct)) return;

        await _db.AirspaceImports.Where(i => i.IsCurrent && i.Id != importId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsCurrent, false), ct);
        await _db.AirspaceImports.Where(i => i.Id == importId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsCurrent, true), ct);
    }

    public async Task DeleteAsync(int importId, CancellationToken ct = default)
    {
        // ⚠️ La guardia si chiede AsNoTracking, e non all'entità tracciata. `SetCurrentAsync` e `SaveAsync`
        // spengono il flag con `ExecuteUpdate`, che scrive nel DATABASE e NON aggiorna il change tracker:
        // un'entità già caricata in questo scope continuerebbe a dire di essere in vigore, e l'eliminazione
        // di un caricamento vecchio verrebbe rifiutata con una motivazione falsa. È lo stesso inganno per
        // cui `ExecuteDelete` non si usa nei repo (vedi la nota su RemoveRange).
        var inVigore = await _db.AirspaceImports.AsNoTracking()
            .Where(i => i.Id == importId).Select(i => (bool?)i.IsCurrent).FirstOrDefaultAsync(ct);
        if (inVigore is null) return;

        // Quello in vigore non si elimina: i settori che ne hanno preso la shape resterebbero a citare un
        // volume che non c'è più, e la pagina non saprebbe dire da dove veniva il loro confine.
        if (inVigore.Value)
            throw new Vipi.Application.Aor.ValidationException(Lingua(
                "Il caricamento in vigore non si elimina: mettine un altro in vigore, poi elimina questo.",
                "The upload in force cannot be deleted: put another one in force, then delete this."));

        var caricamento = await _db.AirspaceImports.FirstAsync(i => i.Id == importId, ct);
        _db.AirspaceImports.Remove(caricamento);   // i volumi cadono in cascata
        await _db.SaveChangesAsync(ct);
    }

    private async Task<int?> CurrentIdAsync(CancellationToken ct) =>
        await _db.AirspaceImports.AsNoTracking().Where(i => i.IsCurrent).Select(i => (int?)i.Id)
            .FirstOrDefaultAsync(ct);

    private static string? Taglia(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= max ? s.Trim() : s.Trim()[..max];

    private static AirspaceImportRow Riga(AirspaceImport i) => new(
        i.Id, i.FileName, i.Sha256, i.SizeBytes, i.AiracCycle, i.GeneratedUtc, i.UploadedUtc,
        i.UploadedByName, i.VolumesRead, i.VolumesUsable, i.DuplicateKeys, i.PointCount, i.IsCurrent);

    private static AirspaceVolumeRow Riga(AirspaceVolume v) => new(
        v.Id, v.ImportId, v.Family, v.Name, v.Category, v.AirspaceClass,
        v.BaseDatum, v.BaseFeet, v.BaseRaw, v.TopDatum, v.TopFeet, v.TopRaw,
        v.PolygonJson, v.RingCount, v.PointCount, v.NaturalKey, v.Ordinal,
        v.MinLat, v.MinLon, v.MaxLat, v.MaxLon);
}
