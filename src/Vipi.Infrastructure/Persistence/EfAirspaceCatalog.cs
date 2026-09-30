using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Airspace;
using Vipi.Application.Auth;
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
/// <para>⚠️ <b>Caricare, mettere in vigore ed eliminare chiedono l'Editor qui dentro</b>: la pagina chiama
/// questa classe senza un servizio in mezzo, e prima del 13 settembre 2026 il solo cancello era il bottone
/// (T-060). Le letture restano libere: la pagina pubblica degli spazi aerei le fa per chiunque.</para>
/// </summary>
public sealed class EfAirspaceCatalog : IAirspaceCatalog
{
    private readonly VipiDbContext _db;
    private readonly IEditAuthorizationService _authz;
    private readonly ShapeChangeStamp? _gettone;

    /// <param name="gettone">
    /// Il gettone dei cambi di forma: una correzione cambia le quote che mappa, 3D e stampa disegnano <b>adesso</b>.
    /// ⚠️ Facoltativo perché i test montano questa porta da sé, come per gli agganci.
    /// </param>
    public EfAirspaceCatalog(VipiDbContext db, IEditAuthorizationService authz, ShapeChangeStamp? gettone = null)
    {
        _db = db;
        _authz = authz;
        _gettone = gettone;
    }

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
        _authz.EnsureAtLeast(VipiRole.Editor);
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
                // 🔴 T-054: anche questi vengono dal FILE, e si tagliano alla colonna come l'intestazione — su
                // MariaDB strict un nome più lungo fa fallire l'intero caricamento.
                NaturalKey = AllaColonna(v.NaturalKey, 300),
                Ordinal = v.Ordinal,
                Family = v.Family,
                Name = AllaColonna(v.Name, 200),
                Category = AllaColonna(v.Category, 64),
                AirspaceClass = Taglia(v.AirspaceClass, 4),
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
        // 🔴 T-054 (revisione del 13 settembre 2026): spegnere il precedente e salvare il nuovo stanno nella STESSA
        // transazione. L'`ExecuteUpdate` si scrive subito: se poi il salvataggio falliva, nessun caricamento
        // restava in vigore e il catalogo sembrava vuoto.
        await new EfUnitOfWork(_db).ExecuteInTransactionAsync(async token =>
        {
            await _db.AirspaceImports.Where(i => i.IsCurrent).ExecuteUpdateAsync(
                s => s.SetProperty(i => i.IsCurrent, false), token);

            _db.AirspaceImports.Add(caricamento);
            await _db.SaveChangesAsync(token);
        }, ct);
        return Riga(caricamento);
    }

    public async Task<IReadOnlyList<AirspaceVolumeRow>> ListVolumesAsync(
        AirspaceVolumeQuery query, CancellationToken ct = default)
    {
        var importId = query.ImportId ?? await CurrentIdAsync(ct);
        if (importId is null) return Array.Empty<AirspaceVolumeRow>();

        var q = _db.AirspaceVolumes.AsNoTracking().Where(v => v.ImportId == importId);
        var correzioni = await CorrezioniAsync(ct);

        // ⚠️ Il filtro per famiglia vale sulla famiglia CORRETTA: in SQL si lasciano passare anche i volumi a cui
        // una correzione cambia il tipo, e si rifiltra dopo averla applicata. Un CTR che il file chiama CTA
        // deve comparire fra i CTR, e sparire dalle CTA.
        IReadOnlyCollection<AirspaceFamily>? filtro = query.Families is { Count: > 0 } famiglie ? famiglie
            : query.UsableOnly ? AirspaceFamilies.Usable : null;
        if (filtro is not null)
        {
            var cambiaTipo = correzioni.Values.Where(c => c.Family is not null).Select(c => c.VolumeKey).ToList();
            q = q.Where(v => filtro.Contains(v.Family) || cambiaTipo.Contains(v.NaturalKey));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var testo = query.Search.Trim();
            q = q.Where(v => EF.Functions.Like(v.Name, $"%{testo}%"));
        }

        var quanti = Math.Clamp(query.Take, 1, 5000);
        var righe = await q.OrderBy(v => v.Family).ThenBy(v => v.Name).ThenBy(v => v.Ordinal)
            .Take(quanti + correzioni.Count).ToListAsync(ct);
        return righe.Select(v => Riga(v, correzioni))
            .Where(v => filtro is null || filtro.Contains(v.Family))
            // In colonna la famiglia è una STRINGA, e l'ordine di sempre è quello alfabetico del nome dell'enum.
            .OrderBy(v => v.Family.ToString(), StringComparer.Ordinal)
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase).ThenBy(v => v.Ordinal)
            .Take(quanti).ToList();
    }

    public async Task<IReadOnlyList<AirspaceVolumeRow>> GetVolumesAsync(
        IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return Array.Empty<AirspaceVolumeRow>();

        var righe = await _db.AirspaceVolumes.AsNoTracking().Where(v => ids.Contains(v.Id)).ToListAsync(ct);
        var indice = righe.ToDictionary(v => v.Id);
        var correzioni = await CorrezioniAsync(ct);

        var esito = new List<AirspaceVolumeRow>(ids.Count);
        foreach (var id in ids)
            if (indice.TryGetValue(id, out var v))
                esito.Add(Riga(v, correzioni));
        return esito;   // ⚠️ NELL'ORDINE CHIESTO: è l'ordine in cui i volumi sono stati agganciati.
    }

    public async Task<IReadOnlyDictionary<AirspaceFamily, int>> CountByFamilyAsync(
        int? importId = null, CancellationToken ct = default)
    {
        var id = importId ?? await CurrentIdAsync(ct);
        if (id is null) return new Dictionary<AirspaceFamily, int>();

        var conti = await _db.AirspaceVolumes.AsNoTracking().Where(v => v.ImportId == id)
            .GroupBy(v => v.Family).Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N, ct);

        // Un volume a cui la correzione cambia il tipo si conta nella famiglia corretta, come lo mostra l'elenco.
        var cambiaTipo = (await CorrezioniAsync(ct)).Values.Where(c => c.Family is not null).ToList();
        if (cambiaTipo.Count == 0) return conti;

        var chiavi = cambiaTipo.Select(c => c.VolumeKey).ToList();
        var toccati = await _db.AirspaceVolumes.AsNoTracking()
            .Where(v => v.ImportId == id && chiavi.Contains(v.NaturalKey))
            .Select(v => new { v.NaturalKey, v.Ordinal, v.Family }).ToListAsync(ct);
        foreach (var v in toccati)
        {
            var c = cambiaTipo.FirstOrDefault(c => c.VolumeKey == v.NaturalKey && c.VolumeOrdinal == v.Ordinal);
            if (c?.Family is not { } nuova || nuova == v.Family) continue;
            if (--conti[v.Family] == 0) conti.Remove(v.Family);
            conti[nuova] = conti.GetValueOrDefault(nuova) + 1;
        }
        return conti;
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
        _authz.EnsureAtLeast(VipiRole.Editor);
        if (!await _db.AirspaceImports.AnyAsync(i => i.Id == importId, ct)) return;

        // T-054: i due passi insieme, o in mezzo resta un catalogo senza niente in vigore.
        await new EfUnitOfWork(_db).ExecuteInTransactionAsync(async token =>
        {
            await _db.AirspaceImports.Where(i => i.IsCurrent && i.Id != importId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsCurrent, false), token);
            await _db.AirspaceImports.Where(i => i.Id == importId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsCurrent, true), token);
        }, ct);
    }

    public async Task DeleteAsync(int importId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
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

    // --- Correzioni a mano (carta docs/feature/2026-09-30-correzioni-spazi-aerei.md) ------------------------------

    public async Task<IReadOnlyList<AirspaceCorrectionRow>> ListCorrectionsAsync(CancellationToken ct = default) =>
        (await _db.AirspaceVolumeCorrections.AsNoTracking()
            .OrderBy(c => c.Name).ThenBy(c => c.VolumeOrdinal).ToListAsync(ct))
        .Select(Riga).ToList();

    public async Task<IReadOnlyList<AirspaceCorrectionFinding>> ReviewCorrectionsAsync(CancellationToken ct = default)
    {
        var correzioni = await ListCorrectionsAsync(ct);
        if (correzioni.Count == 0) return Array.Empty<AirspaceCorrectionFinding>();

        // Servono solo i volumi che una correzione può ritrovare: per chiave, o per nome (e allora tutti gli
        // omonimi, perché il nome vale solo se è unico).
        var importId = await CurrentIdAsync(ct);
        var chiavi = correzioni.Select(c => c.VolumeKey).Distinct().ToList();
        var nomi = correzioni.Select(c => c.Name).Distinct().ToList();
        var volumi = importId is null
            ? new List<AirspaceVolumeRow>()
            : (await _db.AirspaceVolumes.AsNoTracking()
                .Where(v => v.ImportId == importId && (chiavi.Contains(v.NaturalKey) || nomi.Contains(v.Name)))
                .ToListAsync(ct)).Select(Riga).ToList();

        return AirspaceCorrections.Review(correzioni, volumi);
    }

    public async Task CorrectAsync(AirspaceVolumeKey volume, AirspaceCorrectionInput input, int? userId,
        string? userName, DateTime nowUtc, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        if (AirspaceCorrections.Validate(input) is { } errore)
            throw new Vipi.Application.Aor.ValidationException(TestoErrore(errore));

        // Si corregge il volume del file IN VIGORE: la correzione ricorda che cosa diceva quel file.
        var importId = await CurrentIdAsync(ct);
        var file = importId is null ? null : await _db.AirspaceVolumes.AsNoTracking().FirstOrDefaultAsync(
            v => v.ImportId == importId && v.NaturalKey == volume.Key && v.Ordinal == volume.Ordinal, ct);
        if (file is null)
            throw new Vipi.Application.Aor.ValidationException(Lingua(
                "Questo volume non è nel file in vigore: ricarica la pagina.",
                "This volume is not in the file in force: reload the page."));

        var voluta = AirspaceCorrections.Desired(Riga(file), input, 0, nowUtc, Taglia(userName, 128));
        var esistente = await _db.AirspaceVolumeCorrections.FirstOrDefaultAsync(
            c => c.VolumeKey == volume.Key && c.VolumeOrdinal == volume.Ordinal, ct);

        if (voluta is null)
        {
            // Uguale al file in tutto: non c'è niente da correggere, e una correzione vuota sarebbe rumore.
            if (esistente is not null) _db.AirspaceVolumeCorrections.Remove(esistente);
        }
        else
        {
            if (esistente is null)
            {
                esistente = new AirspaceVolumeCorrection { CreatedUtc = nowUtc };
                _db.AirspaceVolumeCorrections.Add(esistente);
            }
            Scrivi(esistente, voluta, userId);
        }

        await _db.SaveChangesAsync(ct);
        _gettone?.Touch();
    }

    public async Task RemoveCorrectionAsync(int correctionId, CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var correzione = await _db.AirspaceVolumeCorrections.FirstOrDefaultAsync(c => c.Id == correctionId, ct);
        if (correzione is null) return;

        // Se il file aveva cambiato la chiave, «prendi il file» vuol dire prendere anche il volume nuovo: gli
        // agganci lo seguono, o resterebbero scoperti proprio dopo che si è deciso che il file va bene.
        var esito = await EsitoAsync(correctionId, ct);
        if (esito is { KeyChanged: true, Volume: { } nuovo })
            await SpostaAgganciAsync(correzione.VolumeKey, correzione.VolumeOrdinal, nuovo, ct);

        _db.AirspaceVolumeCorrections.Remove(correzione);
        await _db.SaveChangesAsync(ct);
        _gettone?.Touch();
    }

    public async Task AcknowledgeCorrectionAsync(int correctionId, int? userId, string? userName, DateTime nowUtc,
        CancellationToken ct = default)
    {
        _authz.EnsureAtLeast(VipiRole.Editor);
        var correzione = await _db.AirspaceVolumeCorrections.FirstOrDefaultAsync(c => c.Id == correctionId, ct);
        if (correzione is null) return;

        var esito = await EsitoAsync(correctionId, ct);
        if (esito is null) return;   // niente da segnalare, niente da confermare

        if (esito is { KeyChanged: true, Volume: { } nuovo })
            await SpostaAgganciAsync(correzione.VolumeKey, correzione.VolumeOrdinal, nuovo, ct);

        // Il file è cambiato e la correzione resta: si riallinea al file nuovo — i campi corretti restano quelli
        // voluti, quelli non corretti seguono il file — e ricorda il file nuovo come «quel che il file diceva».
        // Negli altri due casi la correzione non ha più niente da correggere, e si toglie.
        var voluta = esito is { Kind: AirspaceCorrectionFindingKind.FileChanged, Volume: { } v }
            ? AirspaceCorrections.Desired(v, InputDa(AirspaceCorrections.Apply(v, esito.Correction)),
                correctionId, nowUtc, Taglia(userName, 128))
            : null;

        if (voluta is null) _db.AirspaceVolumeCorrections.Remove(correzione);
        else Scrivi(correzione, voluta, userId);

        await _db.SaveChangesAsync(ct);
        _gettone?.Touch();
    }

    private async Task<AirspaceCorrectionFinding?> EsitoAsync(int correctionId, CancellationToken ct) =>
        (await ReviewCorrectionsAsync(ct)).FirstOrDefault(f => f.Correction.Id == correctionId);

    /// <summary>
    /// Gli agganci che citavano la chiave vecchia passano al volume ritrovato. Un settore che aveva già anche il
    /// volume nuovo perde solo il doppione.
    /// </summary>
    private async Task SpostaAgganciAsync(string chiave, int ordinale, AirspaceVolumeRow nuovo, CancellationToken ct)
    {
        var agganci = await _db.SectorAirspaceBindings
            .Where(b => b.VolumeKey == chiave && b.VolumeOrdinal == ordinale).ToListAsync(ct);
        foreach (var b in agganci)
        {
            var doppione = await _db.SectorAirspaceBindings.AnyAsync(x => x.Catalog == b.Catalog
                && x.SectorId == b.SectorId && x.VolumeKey == nuovo.NaturalKey && x.VolumeOrdinal == nuovo.Ordinal, ct);
            if (doppione) _db.SectorAirspaceBindings.Remove(b);
            else
            {
                b.VolumeKey = nuovo.NaturalKey;
                b.VolumeOrdinal = nuovo.Ordinal;
            }
        }
    }

    private static AirspaceCorrectionInput InputDa(AirspaceVolumeRow v) =>
        new(v.Family, v.AirspaceClass, v.BaseRaw, v.TopRaw);

    private static void Scrivi(AirspaceVolumeCorrection e, AirspaceCorrectionRow r, int? userId)
    {
        e.VolumeKey = AllaColonna(r.VolumeKey, 300);
        e.VolumeOrdinal = r.VolumeOrdinal;
        e.Name = AllaColonna(r.Name, 200);
        e.Family = r.Family;
        e.ClassCorrected = r.ClassCorrected;
        e.AirspaceClass = Taglia(r.AirspaceClass, 4);
        e.BaseRaw = Taglia(r.BaseRaw, 32);
        e.TopRaw = Taglia(r.TopRaw, 32);
        e.FileFamily = r.FileFamily;
        e.FileClass = Taglia(r.FileClass, 4);
        e.FileBaseRaw = AllaColonna(r.FileBaseRaw, 32);
        e.FileTopRaw = AllaColonna(r.FileTopRaw, 32);
        e.UpdatedUtc = r.UpdatedUtc;
        e.UpdatedByUserId = userId;
        e.UpdatedByName = r.UpdatedByName;
    }

    private static string TestoErrore(string codice) => codice switch
    {
        "Class" => Lingua("La classe è una lettera da A a G, o vuota.", "The class is a letter from A to G, or empty."),
        "Base" => Lingua("La base non si legge: GND, 1500 FT AMSL, 1000 FT AGL, FL95.",
            "The base cannot be read: GND, 1500 FT AMSL, 1000 FT AGL, FL95."),
        "Top" => Lingua("Il tetto non si legge: 2500 FT AMSL, 1000 FT AGL, FL195, UNL.",
            "The top cannot be read: 2500 FT AMSL, 1000 FT AGL, FL195, UNL."),
        "Order" => Lingua("La base deve stare sotto il tetto.", "The base must be below the top."),
        "Length" => Lingua("Una quota si scrive in al massimo 32 caratteri.", "A level is at most 32 characters."),
        _ => Lingua("Il tipo non è valido.", "The type is not valid."),
    };

    /// <summary>Le correzioni, per chiave e ordinale: una tabella piccola, letta intera a ogni domanda.</summary>
    private async Task<IReadOnlyDictionary<(string, int), AirspaceCorrectionRow>> CorrezioniAsync(CancellationToken ct) =>
        (await _db.AirspaceVolumeCorrections.AsNoTracking().ToListAsync(ct))
        .ToDictionary(c => (c.VolumeKey, c.VolumeOrdinal), c => Riga(c));

    private static AirspaceVolumeRow Riga(AirspaceVolume v, IReadOnlyDictionary<(string, int), AirspaceCorrectionRow> correzioni) =>
        AirspaceCorrections.Apply(Riga(v), correzioni.GetValueOrDefault((v.NaturalKey, v.Ordinal)));

    internal static AirspaceCorrectionRow Riga(AirspaceVolumeCorrection c) => new(
        c.Id, c.VolumeKey, c.VolumeOrdinal, c.Name, c.Family, c.ClassCorrected, c.AirspaceClass, c.BaseRaw, c.TopRaw,
        c.FileFamily, c.FileClass, c.FileBaseRaw, c.FileTopRaw, c.UpdatedUtc, c.UpdatedByName);

    private async Task<int?> CurrentIdAsync(CancellationToken ct) =>
        await _db.AirspaceImports.AsNoTracking().Where(i => i.IsCurrent).Select(i => (int?)i.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>Un campo obbligatorio tagliato alla colonna, <b>senza</b> ritoccarlo altrimenti: la chiave naturale
    /// deve restare quella che il lettore ha composto.</summary>
    private static string AllaColonna(string? s, int max) =>
        s is null ? "" : s.Length <= max ? s : s[..max];

    private static string? Taglia(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length <= max ? s.Trim() : s.Trim()[..max];

    private static AirspaceImportRow Riga(AirspaceImport i) => new(
        i.Id, i.FileName, i.Sha256, i.SizeBytes, i.AiracCycle, i.GeneratedUtc, i.UploadedUtc,
        i.UploadedByName, i.VolumesRead, i.VolumesUsable, i.DuplicateKeys, i.PointCount, i.IsCurrent);

    /// <summary>Il volume <b>com'è nel file</b>, senza correzioni.</summary>
    internal static AirspaceVolumeRow Riga(AirspaceVolume v) => new(
        v.Id, v.ImportId, v.Family, v.Name, v.Category, v.AirspaceClass,
        v.BaseDatum, v.BaseFeet, v.BaseRaw, v.TopDatum, v.TopFeet, v.TopRaw,
        v.PolygonJson, v.RingCount, v.PointCount, v.NaturalKey, v.Ordinal,
        v.MinLat, v.MinLon, v.MaxLat, v.MaxLon);
}
